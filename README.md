# TRACE

[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.20739404.svg)](https://doi.org/10.5281/zenodo.20739404)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**TRACE** — Timestamped Record of Agentic Chain Execution.

*Timestamp. Record. Append. Compose. Execute.*

An append-only execution record for the REACH + OCTO ecosystem. Where REACH reaches and OCTO orchestrates, TRACE remembers — every arm, every chain, every human decision, every artifact, every failure. Nothing is reconstructed. Nothing is inferred. Everything that happened is written down as it happened.

---

## The Idea in One Paragraph

REACH produces findings. OCTO surfaces decisions. Neither remembers what it did. TRACE is the memory layer — a flat, append-only NDJSON file written alongside your `.reach` and `.octo` files. Every event in every run appends one line. Claude reads those lines on demand through an MCP server and can reconstruct, visualize, query, or audit any run without re-executing it. No database. No dashboard. No always-on process. Just a file and a reader.

---

## Where TRACE Fits

```
REACH    →  reaches into systems, surfaces findings
OCTO     →  coordinates arms, generates decision surface
TRACE    →  records everything that happened, immutably
MCP      →  lets Claude read TRACE and render on demand
```

Each does one thing. Each is a flat file or a single `.cs`. None requires the others to exist. Together they compose into a fully auditable, zero-infrastructure workflow practice.

---

## The File

```
reach-trace.ndjson
```

One JSON object per line. Append-only. Never modified after writing. Lives in the same directory as your `.reach` and `.octo` files, or in a `.trace/` subfolder for per-run isolation.

Every line is a **TRACE event**.

---

## Event Schema

Every TRACE event — regardless of type — contains these fields:

```json
{
  "trace_id":   "uuid-v4",
  "run_id":     "uuid-v4",
  "timestamp":  "ISO 8601 UTC",
  "event_type": "ARM_START",
  "source":     "morning.octo",
  "payload":    {}
}
```

| Field        | Type   | Description                                                    |
|--------------|--------|----------------------------------------------------------------|
| `trace_id`   | string | Unique ID for this event                                       |
| `run_id`     | string | Groups all events from a single REACH or OCTO execution        |
| `timestamp`  | string | ISO 8601 UTC — when the event occurred                         |
| `event_type` | string | One of the event vocabulary below                              |
| `source`     | string | The `.reach` or `.octo` file that triggered this event         |
| `payload`    | object | Event-specific data — see vocabulary below                     |

---

## Event Vocabulary

A closed set of twelve types. Every event in a TRACE file is one of these.

### Execution events

**`ARM_START`** — A REACH arm begins executing.
```json
{
  "event_type": "ARM_START",
  "payload": {
    "arm_name":   "outlook.inbox",
    "script":     "task_a3f2.cs",
    "mode":       "READ"
  }
}
```

**`ARM_COMPLETE`** — A REACH arm finishes successfully.
```json
{
  "event_type": "ARM_COMPLETE",
  "payload": {
    "arm_name":     "outlook.inbox",
    "duration_ms":  1240,
    "exit_code":    0,
    "finding_summary": "3 flagged items, 1 urgent thread"
  }
}
```

**`ARM_RETRY`** — A REACH arm failed and Claude is retrying (compile error or runtime error).
```json
{
  "event_type": "ARM_RETRY",
  "payload": {
    "arm_name":    "outlook.inbox",
    "attempt":     2,
    "reason":      "compile_error",
    "error":       "CS0246: The type 'Outlook' could not be found"
  }
}
```

**`ARM_FAIL`** — A REACH arm failed after all retry attempts.
```json
{
  "event_type": "ARM_FAIL",
  "payload": {
    "arm_name":   "outlook.inbox",
    "attempts":   3,
    "final_error": "Runtime error: MAPI not available"
  }
}
```

### Artifact events

**`ARTIFACT_WRITTEN`** — A `.reach-artifact` or other output file was written.
```json
{
  "event_type": "ARTIFACT_WRITTEN",
  "payload": {
    "artifact_path": "sprint-review-2026-06-16.reach-artifact",
    "artifact_type": "reach-artifact",
    "produced_by":   "sprint-review.reach",
    "size_bytes":    1842
  }
}
```

### Human decision events

**`PAUSE_SURFACED`** — OCTO or a REACH arm surfaced a decision interface to the human.
```json
{
  "event_type": "PAUSE_SURFACED",
  "payload": {
    "title":   "Good morning. Here's what needs you.",
    "items":   ["Sarah emailed twice", "Planner item 3 days overdue"],
    "actions": ["draft-reply", "snooze", "show-all"]
  }
}
```

**`HUMAN_DECIDED`** — The human made a choice at the decision surface.
```json
{
  "event_type": "HUMAN_DECIDED",
  "payload": {
    "action_chosen":  "draft-reply",
    "time_to_decide_ms": 14200
  }
}
```

**`ACTION_TAKEN`** — An ACT-mode reach executed following a human decision.
```json
{
  "event_type": "ACTION_TAKEN",
  "payload": {
    "action":   "draft-reply",
    "target":   "outlook.draft",
    "outcome":  "Draft created — Sarah thread",
    "exit_code": 0
  }
}
```

### Chain events

**`CHAIN_TRIGGERED`** — The output of one arm became the input of another.
```json
{
  "event_type": "CHAIN_TRIGGERED",
  "payload": {
    "from_arm":  "git.commits",
    "to_arm":    "timesystem.timesheet",
    "via":       "analyze.summary"
  }
}
```

### Session events

**`OCTO_CLOSED`** — An OCTO orchestration completed and closed.
```json
{
  "event_type": "OCTO_CLOSED",
  "payload": {
    "arms_completed": 4,
    "arms_failed":    0,
    "close_tone":     "warm",
    "close_message":  "Sarah will appreciate that. One less thing before your first meeting."
  }
}
```

**`REACH_CLOSED`** — A standalone REACH execution completed.
```json
{
  "event_type": "REACH_CLOSED",
  "payload": {
    "duration_ms":  3410,
    "exit_state":   "complete"
  }
}
```

**`TRACE_ANNOTATED`** — A human or Claude added a note to a run after the fact.
```json
{
  "event_type": "TRACE_ANNOTATED",
  "payload": {
    "run_id": "uuid-of-run-being-annotated",
    "note":   "This was the deploy that caused the incident — see post-mortem"
  }
}
```

---

## A Complete TRACE File (Morning Brief)

See [examples/morning-brief.ndjson](examples/morning-brief.ndjson) for the full annotated example.

---

## The MCP Server — `reef-mcp`

A TypeScript MCP server. Run it once. Leave it running.

```
npx @semanticintent/reef-mcp
```

### Five tools. The complete surface.

The MCP does not interpret, route, diff, visualize, or summarize. Claude does all of that. The MCP's job is to surface the right slice of TRACE when Claude needs it, write events reliably when something runs, execute arms on demand, and — crucially — author new arms and assemble them into OCTO workflows on the fly. Everything else — comparisons, timelines, diagrams, summaries, answers to questions, the shape of the next workflow — is Claude reasoning over what the tools return and deciding what to generate next.

---

**`read_trace`** — The main retrieval tool. Returns matching TRACE events as NDJSON.

```
read_trace(
  source?:     string,   // "outlook.inbox", "git.*", "teams.planner"
  since?:      string,   // "2026-05-01", "last-week", "3-months-ago"
  until?:      string,   // "2026-06-01", "today"
  event_type?: string,   // "ARM_COMPLETE", "HUMAN_DECIDED", "ARTIFACT_WRITTEN"
  run_id?:     string    // a specific run
) → TraceEvent[]
```

Any combination of parameters. Returns everything that matches. Claude decides what to do with it — summarize, diff, visualize, answer a question, render a timeline. The tool has no opinion.

---

**`write_trace`** — The canonical write path. Appends a single TRACE event.

```
write_trace(event: TraceEvent) → void
```

All TRACE writes go through here — from generated `.cs` scripts, from OCTO orchestration, from Claude acting on a decision. Makes the write contract enforceable and the flush guarantee uniform. Claude never writes to the NDJSON file directly.

---

**`run_arm`** — Executes a `.reach` or `.octo` file and writes TRACE events as a side effect.

```
run_arm(
  arm_name:   string,   // "outlook.inbox", "kim-paul-compare.octo"
  source:     string,   // which .reach or .octo file this belongs to
  mode:       string,   // "READ" or "ACT"
  intent?:    string,   // optional override — "filter by Kim, last 3 months"
  run_id?:    string    // attach to an existing run, or generate new
) → run_id
```

`ARM_START` is written before execution begins. `ARM_COMPLETE`, `ARM_RETRY`, or `ARM_FAIL` is written when it ends. Accepts both individual `.reach` arms and full `.octo` orchestrations. Claude never has to remember to write events — `run_arm` does it unconditionally.

---

**`write_arm`** — Authors a `.reach` file from a natural language intent and saves it.

```
write_arm(
  name:    string,   // "kim-inbox.reach"
  intent:  string,   // natural language — Claude compiles to .reach DSL
  source:  string    // which system to reach into
) → arm_name
```

Claude translates the intent into a valid `.reach` file using the REACH DSL. The file is saved alongside existing `.reach` files and is immediately available to `run_arm` and `write_octo`. The authored file is a first-class artifact — git-tracked, shareable, forkable — identical to one written by hand.

---

**`write_octo`** — Assembles a set of arms into an `.octo` orchestration file.

```
write_octo(
  name:    string,     // "kim-paul-compare.octo"
  arms:    string[],   // arm names — mix of existing and just-authored
  surface: string,     // what the decision surface should show and offer
  close?:  string      // tone and summary for OCTO_CLOSED
) → octo_name
```

Claude assembles the named arms into a valid `.octo` file with the specified surface and close behaviour. The file is saved and immediately available to `run_arm`. The output is indistinguishable from a hand-authored `.octo` file.

---

### What the conversation actually looks like

```
User:   show me previous inbox queries

Claude: read_trace(source="outlook.inbox")
        → 12 runs returned
        Here are your inbox queries — project billing (6 months),
        cadence last week, Paul communication (3 months) [and 9 more]

User:   run one for Kim like the Paul query

Claude: read_trace(run_id="paul-run-id")   ← retrieves original intent
        run_arm(arm_name="outlook.inbox", intent="Kim, last 3 months", mode="READ")
        → ARM_START written, script executes, ARM_COMPLETE written
        Here's what came back for Kim — [findings]

User:   how long did that take, what period did it cover

Claude: already in context — no new tool call needed
        That took 1.8 seconds and covered March 16 to June 16.

User:   I want to understand how Kim and Paul's communication
        patterns differ over the last quarter — email, Teams,
        and any shared calendar items

Claude: I'll build that. Authoring five arms and assembling the workflow.

        write_arm("kim-inbox.reach",  intent="outlook.inbox, Kim, 90 days")
        write_arm("paul-inbox.reach", intent="outlook.inbox, Paul, 90 days")
        write_arm("kim-teams.reach",  intent="teams.messages, Kim, 90 days")
        write_arm("paul-teams.reach", intent="teams.messages, Paul, 90 days")
        write_arm("shared-cal.reach", intent="outlook.calendar, Kim+Paul overlap")

        write_octo("kim-paul-compare.octo",
                   arms=["kim-inbox", "paul-inbox", "kim-teams",
                         "paul-teams", "shared-cal"],
                   surface="side-by-side communication comparison",
                   close="warm")

        run_arm("kim-paul-compare.octo", mode="READ")
        → all five arms execute in parallel
        → TRACE written throughout — ARM_START ×5, ARM_COMPLETE ×5,
          PAUSE_SURFACED, OCTO_CLOSED
        [renders comparison — frequency, response times,
         shared meetings, tone patterns]
```

The conversation has no constraints because TRACE has no constraints and the MCP can author whatever is needed. Anything that ran left a record. Anything that doesn't exist yet can be written. Claude is the intelligence across all of it.

### The generation path and the hand-authored path are the same

`.reach` and `.octo` files produced by `write_arm` and `write_octo` are identical in structure to files written by hand. They live in the same directory, run through the same `run_arm` tool, and produce the same TRACE events. A workflow assembled by Claude in conversation can be opened, edited, forked, and shared — because it is just a file.

---

## File Conventions

### Single shared file (default)
```
project/
  morning.octo
  sprint-review.reach
  reach-trace.ndjson        ← all runs, all time
```

Good for personal workflow. Simple. Everything in one place. Rotate by month or size as needed.

### Per-run isolation
```
project/
  morning.octo
  .trace/
    r1-morning-2026-06-16.ndjson
    r2-sprint-2026-06-16.ndjson
```

Good for audit scenarios where runs must be independently verifiable. Each file is self-contained. MCP server scans the `.trace/` folder.

---

## Writing Conventions

These conventions are not enforced by a schema validator. They are the contract that makes TRACE trustworthy.

### Write at the moment of occurrence, not at completion

Every event is written when it happens, not when the arm finishes. `ARM_START` is written before `dotnet run` is called. `PAUSE_SURFACED` is written before the human sees the surface. `HUMAN_DECIDED` is written the moment a choice is made.

This is the most important convention. A script that crashes between `ARM_START` and `ARM_COMPLETE` leaves a detectable open event. A script that writes both events only on success leaves a silent gap. Silent gaps are invisible and therefore dangerous.

```
✓  write ARM_START → run script → write ARM_COMPLETE or ARM_FAIL
✗  run script → write ARM_START and ARM_COMPLETE together on success
```

### Never buffer. Flush immediately.

Each event is flushed to disk as a single atomic line append before the next operation begins. NDJSON is line-delimited for this reason — a crash mid-line corrupts at most one event, and the line is identifiably malformed.

### One line, one event, always valid JSON.

No pretty-printing. No multi-line events. Each line must be parseable independently — the MCP server reads the file line by line and skips lines it cannot parse rather than failing.

### Artifact fidelity is the writer's responsibility.

Claude will faithfully reconstruct, visualize, and reason about whatever TRACE contains — including stale, incomplete, or incorrect records. Correctness has two parts: intent preservation (a property of execution) and artifact fidelity (a property of what was written). TRACE can only guarantee the first if writers honour the second.

---

## The Two-Reader Pattern — Semantic Intent in MCP Tools

Each tool in `reef-mcp` carries a Semantic Intent block. The same block is read two ways simultaneously — this is not incidental, it is the design.

**Reader one: the parser.** When `reef-mcp` starts, a deterministic parser reads every tool's Semantic Intent block and extracts the MCP input schema. No model is involved.

**Reader two: Claude.** When Claude calls a tool and receives its output, the output includes the Semantic Intent block as an emitted directive. Claude reads it, recognises an action plan, and executes it — without being told to.

Same block. Two consumers. The consumer — a regex parser or a language model — is the parameter.

The pattern is described in full in [One Grammar, Two Readers](https://semanticintent.dev/writing/one-grammar-two-readers) and formalised in [Semantic Intent as Emitted Directive](https://doi.org/10.5281/zenodo.20563444).

---

## What TRACE Does Not Do

- **No dashboard.** Visualization happens on demand through Claude and the MCP server. Nothing persists on screen.
- **No schema enforcement at write time.** TRACE events are written by Claude-compiled arms — the schema is a convention, not a validator.
- **No event modification.** Append-only is not a preference. It is the design. A TRACE file that has been modified is not a TRACE file.
- **No always-on process.** The MCP server runs when you need it. The trace file exists when you don't.

---

## In the Semantic Intent Ecosystem

| Layer  | Does                                          | Artifact               |
|--------|-----------------------------------------------|------------------------|
| CAL    | Decides what analysis means                   | `.cal` analysis unit   |
| EMBER  | Remembers methodology across sessions         | `.sil` files           |
| REACH  | Reaches into live systems, surfaces findings  | `.reach` + `.cs`       |
| OCTO   | Coordinates arms, generates decision surface  | `.octo`                |
| TRACE  | Records everything that happened, immutably   | `reach-trace.ndjson`   |
| RECALL | Publishes structured documents                | `.rcpy` + HTML         |
| Mere   | Contains — the file is the app                | `.mp.html`             |

TRACE sits between execution and memory. REACH and OCTO write to it. Claude reads from it. EMBER can reference it. CAL can analyze it.

---

## Requirements

- Node.js 20+ (for `reef-mcp`)
- Claude Code CLI or any Claude interface with MCP support
- A `.reach` or `.octo` workflow that appends TRACE events (or any process that writes NDJSON to the convention path)
- .NET 10 SDK — for the REACH arms that execute within the reef

---

## Citation

```bibtex
@misc{shatny2026trace,
  author    = {Shatny, Michael},
  title     = {TRACE: Timestamped Record of Agentic Chain Execution},
  year      = {2026},
  publisher = {Zenodo},
  doi       = {10.5281/zenodo.20739404},
  url       = {https://github.com/semanticintent/trace}
}
```

Part of the [Semantic Intent](https://semanticintent.dev) ecosystem · MIT License

---

*REACH + OCTO + TRACE © 2026 Michael Shatny*
