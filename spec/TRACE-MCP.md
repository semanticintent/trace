# TRACE MCP — Implementation Roadmap

`reef-mcp` — a TypeScript MCP server. Five tools, three phases.

The README specifies *what* each tool does. This document specifies *how to build it* — phases, dependencies, technical decisions, and the Semantic Intent blocks that make the Two-Reader Pattern work.

**Implementation repo:** `@semanticintent/reef-mcp` (TypeScript, npm). The REACH arms that execute within the reef remain .NET 10 C# — the MCP server orchestrates them via child process, it does not replace them.

---

## The Three Phases

```
Phase 1  →  write_trace + read_trace      File I/O. Ship this first.
Phase 2  →  run_arm                        Subprocess + event side-effects.
Phase 3  →  write_arm + write_octo         Authoring. Deferred — DSL-dependent.
```

Each phase is independently useful. Phase 2 depends on Phase 1. Phase 3 depends on Phase 2 and on REACH DSL stability.

---

## Phase 1 — File I/O

### `write_trace`

Accepts a TraceEvent object. Appends it to the NDJSON file as a single atomic line.

**Implementation steps:**

1. Validate required fields: `trace_id`, `run_id`, `timestamp`, `event_type`, `source`, `payload`
2. Serialize to JSON — single line, no pretty-printing, no trailing whitespace
3. Open file in append mode with exclusive lock (`FileShare.None`)
4. Write line + newline
5. Flush immediately — do not buffer
6. Release lock

**Concurrency:** OCTO runs multiple arms simultaneously. Multiple arms may call `write_trace` at the same time. The implementation must use exclusive file locking with retry-on-contention — open the file exclusively, write and flush atomically, release, retry on lock failure (up to 5 attempts, 20ms backoff).

**Why this matters:** An event that fails to write is a silent gap. Silent gaps are invisible and therefore dangerous. The retry loop is the minimum concurrency guarantee.

---

### `read_trace`

Reads the NDJSON file line by line. Applies filters. Returns matching events in chronological order.

**Implementation steps:**

1. Open file in read mode (`FileShare.ReadWrite` — allow concurrent writes)
2. Read line by line — `File.ReadLines()` or `StreamReader`
3. Skip lines that fail JSON parse — log a warning, do not throw
4. Apply filters in order: `run_id` → `event_type` → `source` → `since` → `until`
5. Return matching events as a JSON array

**Filter implementations:**

```
run_id       →  exact string match on event.run_id
event_type   →  exact string match on event.event_type
source       →  glob match — "git.*" matches "git.commits", "git.tags"
since        →  parse relative or absolute → compare event.timestamp
until        →  parse relative or absolute → compare event.timestamp
```

**Source glob matching** — simple wildcard only (`*`): split pattern on `*`, check each segment appears in order within the value. `"git.*"` matches `"git.commits"`, `"git.tags"`.

**Relative time parsing** — the terms Claude uses in conversation:

```
"today"          →  start of current UTC day
"yesterday"      →  start of previous UTC day
"last-week"      →  7 days ago from now
"last-month"     →  30 days ago from now
"3-months-ago"   →  90 days ago from now
"6-months-ago"   →  180 days ago from now
"N-days-ago"     →  N days ago (parse N from prefix)
"N-weeks-ago"    →  N*7 days ago
"N-months-ago"   →  N*30 days ago
ISO 8601 string  →  parse directly
```

**Phase 1 deliverable:** A working MCP with two tools. Any REACH arm that calls `write_trace` after each operation is now fully auditable. Claude can query run history, reconstruct timelines, diff two runs, answer "what did we do last week." This is immediately useful with zero changes to the REACH runtime.

---

## Phase 2 — Arm Execution

### `run_arm`

Executes a `.reach` or `.octo` file. Writes TRACE events unconditionally as side effects. Returns a `run_id`.

**Implementation steps:**

1. Generate `run_id` if not provided: `crypto.randomUUID()`
2. Resolve the arm file — look in current directory and standard arm paths
3. Write `ARM_START` event via `write_trace`
4. Dispatch by file type:
   - `.reach` file → compile and run (see below)
   - `.octo` file → parse and orchestrate (see below)
5. On success: write `ARM_COMPLETE` with `duration_ms` and `finding_summary`
6. On failure: write `ARM_FAIL` with `attempts` and `final_error`
7. Return `run_id`

**Compile-and-run loop for `.reach` files:**

```
attempt = 1
loop:
  generate C# script from .reach DSL (Claude)
  write tmp_<id>.cs to disk
  run: dotnet run tmp_<id>.cs
  capture stdout + stderr
  if exit_code == 0:
    write ARM_COMPLETE
    break
  if compile error or runtime error AND attempt < MAX_RETRIES:
    write ARM_RETRY (attempt, reason, error)
    attempt++
    continue
  else:
    write ARM_FAIL
    break
```

`MAX_RETRIES` — default 3. Configurable per arm via intent override.

**PAUSE handling:**

When mode is `ACT` and the arm contains a PAUSE gate:

```
write PAUSE_SURFACED (title, items, actions)
wait for human input (stdin or MCP tool call)
write HUMAN_DECIDED (action_chosen, time_to_decide_ms)
continue execution with chosen action
write ACTION_TAKEN
```

**`.octo` orchestration:**

Parse the `.octo` file. Extract the `arms` block. Run each arm via `run_arm` in parallel (Task.WhenAll). When all complete, synthesize the combined signal, generate the surface, write `PAUSE_SURFACED`. On human decision: write `HUMAN_DECIDED`, dispatch ACT arms. Write `OCTO_CLOSED` when the session closes.

**Chain detection:**

When the output of one arm is explicitly passed as input to another (via the `via` field in the `.octo` arms block), write `CHAIN_TRIGGERED` between `ARM_COMPLETE` of the source and `ARM_START` of the target.

**Phase 2 dependency:** Requires the REACH compilation model — Claude must be reachable from within `trace.mcp.cs` to generate and fix C# scripts. The MCP server calls Claude; Claude does not call itself. This is the integration seam between TRACE and REACH.

**Phase 2 deliverable:** A fully working agentic execution loop where every event — start, retry, completion, human decision, action — is written to TRACE automatically. No arm author needs to think about events.

---

## Phase 3 — Authoring

### `write_arm`

Translates a natural language intent into a `.reach` file and saves it.

**Implementation steps:**

1. Take `name`, `intent`, `source`
2. Prompt Claude with the REACH DSL spec + the intent
3. Claude generates a valid `.reach` file
4. Save to the arm directory alongside existing `.reach` files
5. Return `arm_name`

**Dependency:** REACH DSL must be stable and comprehensive enough for Claude to compile to reliably. Run this phase only after `REACH-DSL.md` is considered stable. A generated `.reach` file that runs through `run_arm` and fails silently is worse than no authoring tool at all.

---

### `write_octo`

Assembles a set of named arms into an `.octo` orchestration file.

**Implementation steps:**

1. Take `name`, `arms[]`, `surface`, `close?`
2. Verify each named arm exists on disk
3. Generate `.octo` file with valid syntax:
   - `arms` block listing each arm with default qualifiers
   - `surface` block with the specified display intent
   - `close` block with the specified tone
4. Save to the same directory as the named arms
5. Return `octo_name`

**Dependency:** Phase 2 must be working — the generated `.octo` file is immediately runnable via `run_arm`.

**Phase 3 deliverable:** Full conversational arm authoring. A user can describe what they want, Claude builds the workflow from scratch, runs it, and TRACE records everything. The generation path and the hand-authored path are the same.

---

## Technical Decisions

### MCP Protocol

Use the `@modelcontextprotocol/sdk` npm package (TypeScript). Handles stdio transport, tool registration, and schema generation. Each tool is registered with a `server.tool()` call. The Semantic Intent block lives in the tool description — parsed by the MCP loader at startup, returned in tool output for Claude to read.

See `@semanticintent/reef-mcp` for the implementation.

### File Path Resolution

Default trace file: `./reach-trace.ndjson` — relative to the working directory where `reef-mcp` is started.

Configurable via environment variable: `TRACE_FILE=path/to/custom.ndjson`

Per-run isolation: if `TRACE_DIR` is set, write to `$TRACE_DIR/<run_id>.ndjson` instead.

### UUID Generation

```typescript
const traceId = crypto.randomUUID()
const runId   = crypto.randomUUID()
```

### Timestamp Format

```typescript
const timestamp = new Date().toISOString()  // always UTC, ISO 8601
```

---

## Semantic Intent Blocks

Each tool carries a Semantic Intent block. The same block is parsed at startup to build the MCP input schema, and returned in tool output for Claude to read as an action plan.

The block lives in the tool description. Format:

```
I return [what this tool produces].

I need:   param (type, required|optional), param (type, required|optional)
I will:   [what I do, in plain language]
I return: [the shape of the output]
I estimate: [complexity signal — low | medium | variable]
```

### `read_trace`

```
I return matching TRACE events from reach-trace.ndjson as raw NDJSON.

I need:   source (string, optional), since (string, optional),
          until (string, optional), event_type (string, optional),
          run_id (string, optional)
I will:   filter reach-trace.ndjson by any combination of the above,
          return all matching events in chronological order,
          skip malformed lines without failing
I return: NDJSON — one TraceEvent per line, everything that matched
I estimate: low complexity, 200-600 tokens depending on range
```

### `write_trace`

```
I append a single TRACE event to reach-trace.ndjson, atomically.

I need:   event (TraceEvent, required) — must include trace_id, run_id,
          timestamp, event_type, source, payload
I will:   validate required fields, serialize to a single JSON line,
          acquire an exclusive file lock, append and flush, release lock
I return: void — throws on write failure after retries exhausted
I estimate: low complexity, write-only, no output tokens
```

### `run_arm`

```
I execute a REACH arm and write TRACE events unconditionally as side effects.

I need:   arm_name (string, required), source (string, required),
          mode (string, required — READ or ACT), intent (string, optional),
          run_id (string, optional)
I will:   write ARM_START before execution, compile and run the arm,
          write ARM_RETRY on each error, write ARM_COMPLETE or ARM_FAIL
          when execution ends, surface a PAUSE if mode is ACT and the
          arm requests human confirmation before proceeding
I return: run_id — the identifier for this execution in TRACE
I estimate: variable — depends on arm complexity and retry count
```

### `write_arm`

```
I author a .reach file from natural language intent and save it to disk.

I need:   name (string, required), intent (string, required),
          source (string, required)
I will:   translate intent into valid .reach DSL using the REACH spec,
          save the file alongside existing .reach files,
          return the arm name for use in run_arm or write_octo
I return: arm_name — the filename (without path) of the saved .reach file
I estimate: medium complexity, DSL compilation quality determines correctness
```

### `write_octo`

```
I assemble named arms into an .octo orchestration file and save it.

I need:   name (string, required), arms (string[], required),
          surface (string, required), close (string, optional)
I will:   verify each named arm exists on disk, generate a valid .octo file
          with the specified arms, surface intent, and close behaviour,
          save alongside the named arms
I return: octo_name — the filename (without path) of the saved .octo file
I estimate: low-medium complexity, correctness depends on arm availability
```

---

## MCP Server Startup

```typescript
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js'
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js'

const server = new McpServer({ name: 'reef-mcp', version: '0.1.0' })

// register tools: read_trace, write_trace, run_arm, write_arm, write_octo
registerTools(server)

const transport = new StdioServerTransport()
await server.connect(transport)
```

All five tools registered at startup. The trace file path resolved from `TRACE_FILE` env var or defaults to `./reach-trace.ndjson`.

---

## Ship Order

```
v0.1.0  →  spec published (done — DOI 10.5281/zenodo.20739404)
v0.2.0  →  Phase 1: write_trace + read_trace
            Any REACH arm can now write to TRACE. Claude can query history.
v0.3.0  →  Phase 2: run_arm
            Full execution loop. Events written automatically.
            OCTO orchestration via TRACE.
v1.0.0  →  Phase 3: write_arm + write_octo
            Full conversational authoring. Requires REACH DSL stability.
```

Each version is independently useful. Nothing in v0.3.0 breaks v0.2.0 consumers.
