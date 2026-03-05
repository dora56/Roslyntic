# Observability — Roslyntic

Roslyntic is a CLI tool, not a long-running service.
Observability here means: the run is explainable, measurable, and debuggable
**without polluting STDOUT** (STDOUT is reserved for SARIF/JSON).

## Non-functional requirement (NF5)
### NF5.1 Channels
- STDOUT: SARIF 2.1.0 or JSON only (strict).
- STDERR: human-readable progress, warnings, errors, timing summary.
- Optional: `--log-file <path>` to persist logs (recommended for CI).
- Optional: `--log-format text|json` (default: text).

### NF5.2 Minimum signals (must emit)
Emit the following signals to STDERR and/or log file:

**Timing**
- total duration
- phase durations:
    - workspace load
    - rule execution
    - output write

**Scale**
- projects count
- documents count

**Quality signals**
- rules executed count
- findings count by level (error/warning/note)

**Failure classification**
- workspace load failure
- rule execution failure
- unexpected exception
- (future) plugin timeout / plugin crash

### NF5.3 Structured logs (when enabled)
When `--log-format json` is set:
- output newline-delimited JSON (NDJSON): one JSON object per line
- keys must be stable across versions
- do not include source code content

## Run metrics schema (NDJSON)
Example record (illustrative):

- `event`: `run_start` | `phase_end` | `run_end` | `error`
- `runId`: string
- `toolVersion`: string
- `command`: string
- `path`: string
- `format`: `sarif` | `json`
- `exitCode`: 0 | 1 | 2
- `durMs`: number
- `phase`: `workspace_load` | `rules` | `output_write`
- `counts`: `{ projects, documents, rulesExecuted, findingsError, findingsWarning, findingsNote }`
- `error`: `{ category, message }` (message should be short and non-sensitive)

## KPIs (MVP)
Keep KPIs minimal and actionable.

1) **Adoption**
- CI workflows or repos that run `roslyntic check` weekly (or total runs/week)

2) **Signal quality (noise proxy)**
- findings per run (by severity) and trend
- ratio of suppressed/ignored findings (if suppression exists later)

3) **Agent loop efficiency**
- number of iterations needed for a PR to reach “no errors” (or below threshold)

## SLIs / SLOs (MVP)
These SLOs target **tool quality**, not service reliability.

### SLI: Determinism (most important)
- Definition: Same commit and environment => byte-identical SARIF/JSON across repeated runs.
- SLO: **>= 99.9%**

### SLI: Successful completion
- Definition: `roslyntic check` exits with code 0 or 1 (code 2 indicates tool failure).
- SLO: **>= 99.0%** (MVP)

### SLI: Performance (P95)
Define solution size by number of projects:
- Small (<=10): P95 <= **30s**
- Medium (<=100): P95 <= **3m**
- Large (100+): P95 <= **10m**

### SLI: SARIF validity
- Definition: Output is valid JSON and includes required SARIF fields.
- SLO: **>= 99.9%**

## Measurement approach (privacy-preserving)
- No network/telemetry by default.
- Emit run metrics to STDERR and/or `--log-file` (NDJSON when enabled).
- CI systems may collect logs externally (artifacts or internal observability stack).