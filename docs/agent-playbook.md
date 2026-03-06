# Agent Playbook — Roslyntic

This document is the practical implementation guide for AI agents and humans.

## MVP goal
Implement `roslyntic check <path>` that:
1) Loads a `.sln`, `.slnx`, or `.csproj` using MSBuildWorkspace.
2) Runs built-in rules:
    - `AGARCH0001` Layer violation
    - `AGCOMP0001` Cyclomatic complexity threshold
3) Emits **SARIF 2.1.0** (default) or JSON to STDOUT only.
4) Uses deterministic ordering of diagnostics:
    1) file path (ordinal)
    2) start line
    3) start column
    4) ruleId
5) Exit codes:
    - 0: no findings above threshold
    - 1: findings exist
    - 2: tool execution failure

## CLI contract
### Output streams
- STDOUT: SARIF/JSON only.
- STDERR: progress/errors (workspace load failures, invalid config, etc.)

### Suggested commands (MVP + near future)
- `roslyntic check <path> [--format sarif|json] [--fail-on warning|error]`
- `roslyntic rules init` (Phase 2)
- `roslyntic rules list` (Phase 2)
- `roslyntic explain <ruleId>` (Phase 2)

## Observability
See `docs/observability.md` for the observability requirements and MVP SLI/SLO.

## Repo layout (current)
- `src/Roslyntic.Cli/Program.cs` : entrypoint
- `src/Roslyntic.Cli/Core/` : command contract + workspace loading + pipeline orchestration
- `src/Roslyntic.Cli/Analysis/` : dependency extraction + complexity calculation
- `src/Roslyntic.Cli/Rules/` : built-in rules producing diagnostics
- `src/Roslyntic.Cli/Sarif/` : SARIF 2.1.0 and JSON mapping/writers
- `tests/Roslyntic.Tests/Integration/` : CLI contract/integration tests
- `tests/Roslyntic.Tests/Unit/` : analysis/rule/sarif unit tests
- `tests/Roslyntic.Tests/TestSupport/` : CLI harness and test helpers

## Configuration (optional for MVP)
If needed, add `roslyntic.json` with defaults:
- `format`: `sarif` | `json`
- `failOn`: `warning` | `error`
- `rules`:
    - `AGCOMP0001.threshold` (default 15)
    - `AGARCH0001.forbiddenEdges` (default set)
- `layers`:
    - mapping patterns -> layer name

Keep schema simple and documented.

## Testing requirements
### Unit tests (required)
- Complexity calculator
- Layer classifier (pattern-based)
- SARIF mapping (snapshot / golden file)

### Integration test (required)
- Create `sample/` with a minimal multi-project solution:
    - `Samples.Domain`
    - `Samples.Application`
    - `Samples.Infrastructure`
    - `Samples.UI`
- Intentionally include:
    - a forbidden dependency (UI referencing Infrastructure or Domain directly)
    - a method with complexity > threshold
- Run `roslyntic check sample/Samples.slnx` (or `.sln`) and assert:
    - SARIF is valid JSON
    - expected ruleIds exist
    - ordering is stable

## Performance guidance
- Load the workspace once per run.
- Avoid creating semantic models repeatedly if possible.
- Prefer incremental enumeration over global symbol walks (MVP can be simple).

## Safety guidance (plugins)
Plugins are Phase 2+. When introduced:
- Avoid in-process execution of untrusted plugins.
- Prefer an isolated worker process with timeout and deterministic failure reporting (`AGSAFE9001`).

## Definition of done (MVP)
- `roslyntic check` works on a real `.sln`/`.slnx`/`.csproj`
- SARIF 2.1.0 output is deterministic
- Tests pass (unit + integration)
- README includes usage examples
