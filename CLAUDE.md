# TRACE — Claude Code Context

You are working in the TRACE repository. TRACE is the memory layer for the REACH + OCTO ecosystem.

**TRACE** — Timestamped Record of Agentic Chain Execution.  
*Timestamp. Record. Append. Compose. Execute.*

---

## What TRACE Is

An append-only NDJSON file (`reach-trace.ndjson`) that records every event across every REACH and OCTO run. REACH arms write to it. OCTO orchestrations write to it. Claude reads from it through a five-tool MCP server (`trace.mcp.cs`).

Nothing is reconstructed. Nothing is inferred. Everything that happened is written down as it happened.

---

## The Five MCP Tools

| Tool          | Does                                                        |
|---------------|-------------------------------------------------------------|
| `read_trace`  | Filter and return TRACE events — source, time, type, run_id |
| `write_trace` | Append a single event — the only write path                 |
| `run_arm`     | Execute a `.reach` or `.octo`, writing events as side effect |
| `write_arm`   | Author a new `.reach` file from natural language intent     |
| `write_octo`  | Assemble named arms into an `.octo` orchestration file      |

---

## The Twelve Event Types

```
ARM_START         REACH arm begins
ARM_COMPLETE      REACH arm finishes successfully
ARM_RETRY         compile or runtime error — Claude retrying
ARM_FAIL          arm failed after all retries
ARTIFACT_WRITTEN  .reach-artifact or output file written
PAUSE_SURFACED    decision surface shown to human
HUMAN_DECIDED     human made a choice at the surface
ACTION_TAKEN      ACT-mode execution followed a human decision
CHAIN_TRIGGERED   one arm's output became another arm's input
OCTO_CLOSED       OCTO orchestration completed
REACH_CLOSED      standalone REACH execution completed
TRACE_ANNOTATED   human or Claude added a note to a past run
```

---

## The Most Important Convention

Write at the moment of occurrence, not at completion.

```
✓  write ARM_START → run script → write ARM_COMPLETE or ARM_FAIL
✗  run script → write ARM_START and ARM_COMPLETE together on success
```

An open `ARM_START` with no matching `ARM_COMPLETE` is detectable signal — an arm that started and never closed. A silent gap is invisible and therefore dangerous.

---

## File Layout

```
trace/
  README.md              ← spec and full documentation
  CITATION.cff
  LICENSE
  CLAUDE.md              ← this file
  examples/
    morning-brief.ndjson ← annotated complete example
```

`trace.mcp.cs` — the MCP server implementation — is not yet built. The spec is complete; implementation begins with `write_trace` + `read_trace`.

---

## Related Repos

- `semanticintent/reach` — REACH + OCTO (DOI: 10.5281/zenodo.20680385)
- `semanticintent/intent-as-infrastructure` (DOI: 10.5281/zenodo.20681523)
- `semanticintent/semantic-intent-emitted-directive` (DOI: 10.5281/zenodo.20563444)
