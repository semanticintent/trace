# REACH Arms Roadmap

Arms are single-file C# scripts (`*.cs`) that run via dotnet-script. Each arm reads args from stdin as JSON, writes structured JSON to stdout, and is executed by `run_arm` in reef-mcp — which writes `ARM_START` + `ARM_COMPLETE` / `ARM_FAIL` TRACE events automatically.

Env vars available to every arm: `TRACE_FILE`, `REACH_REPO`, `WORKSPACE`, `ARMS_DIR`, `DOTNET_ROOT`.

---

## Phase 1 — Morning Brief cluster

The first OCTO workflow: a zero-config daily startup brief assembled from independent arms.

| Arm | Sources | Output |
|---|---|---|
| `MORNING-BRIEF` ✅ | `git.commits`, `reef.trace` | commits today + trace event summary |
| `FETCH-MAIL` | `outlook.inbox` | unread count, flagged items, sender list |
| `SYNC-CALENDAR` | `outlook.calendar` | today's meetings, next meeting + time until |
| `REPO-STATUS` | `git.*` | all workspace repos — uncommitted, ahead/behind, stale branches |

**Target `.octo`:** `morning.octo` — runs all four in sequence, chains output into a single brief surface.

---

## Phase 2 — End of day cluster

Closes the loop on what ran, what shipped, what's pending.

| Arm | Sources | Output |
|---|---|---|
| `EOD-SUMMARY` | `reef.trace` | all runs today, arms executed, artifacts written, decisions made |
| `COMMIT-CHECK` | `git.*` | any uncommitted or unpushed work across all workspace repos |
| `ARTIFACT-SCAN` | `reef.trace` | all `ARTIFACT_WRITTEN` events today — what was built |

**Target `.octo`:** `eod.octo` — pairs with `morning.octo` to bracket the day.

---

## Phase 3 — Trading cluster

Connects the reef to the ES/MES trading stack (`spbl-trader`, `fetch-market-data`).

| Arm | Sources | Output |
|---|---|---|
| `MARKET-BRIEF` | `fetch-market-data` (localhost:3100) | ES pre-market — price, volume, overnight range |
| `TRADE-LOG` | `ninjatrader.export` | today's fills, P&L, slippage vs baseline |
| `SPBL-SCAN` | `ninjatrader.strategy` | SPBL signal check — is the 8am condition met? |

**Target `.octo`:** `trading-morning.octo` — market brief + signal check before the open.

---

## Phase 4 — Workspace hygiene

Passive arms that run on-demand, not daily.

| Arm | Sources | Output |
|---|---|---|
| `WORKSPACE-SCAN` | `git.*`, `npm.*` | stale branches, outdated packages, large files |
| `DEP-AUDIT` | `npm.*` | audit across all workspace `package.json` files |
| `TRACE-PRUNE` | `reef.trace` | summarise + optionally archive events older than 30 days |

---

## Conventions (all arms follow these)

```
// SI:
// ARM: ARM-NAME
// INTENT: one line
// SOURCES: dot.path, dot.path
// OUTPUT: JSON — { field, field }
// :SI
```

- Read args from stdin as JSON — fall back to env vars — fall back to defaults
- Always write to stdout as JSON (single `Console.WriteLine`)
- Never throw unhandled — catch and include `{ error: "..." }` in output
- Exit 0 on success, non-zero on fatal failure
- Keep under 150 lines — if it needs more, it should be two arms
```
