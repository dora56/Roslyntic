# Roslyntic
Roslyntic is an agentic, Roslyn-based static analysis CLI for C#.

It provides deterministic, compiler-grade feedback in **SARIF 2.1.0** (and JSON) so AI coding agents
(GitHub Copilot, Aider, Claude Code, Cursor, etc.) can self-correct without hallucinating, while keeping
architecture consistent and quality gates enforceable in CI.

## What it is
A local-only C# static analysis CLI built on Roslyn/MSBuild that:
- Loads `.sln` / `.csproj` via MSBuildWorkspace
- Detects architecture violations and computes complexity metrics
- Emits machine-readable results in **SARIF 2.1.0** (and JSON)
- Supports **Mentorship-as-Code**: project-specific rules implemented as C# plugins

## Key principles
- **Determinism over vibes**: output only facts derived from Roslyn semantics
- **Agent-first I/O**: stable rule IDs, stable ordering, no “pretty” logs
- **Local-only privacy**: no source code is sent to cloud APIs
- **Safe extensibility**: agent-generated rules must not hang or crash the host process

## Commands (planned)
- `roslyntic check <path> [--format sarif|json] [--fail-on warning|error] [--baseline <sarif>]`
- `roslyntic rules init`  (creates starter rule templates)
- `roslyntic rules list`  (shows loaded rules)
- `roslyntic explain <ruleId>` (returns machine-readable help for the rule)
- `roslyntic graph export [--format json|dot]`

## Output formats
### SARIF (default)
- SARIF 2.1.0 with GitHub Code Scanning compatibility in mind
- Stable ordering: path → line → column → ruleId
- Stable fingerprints when possible

### JSON
A simplified schema for local agent loops:
- `diagnostics[]`
- `rules[]`
- `metadata` (tool version, run id, timing)

## Observability
Roslyntic never prints logs to STDOUT (STDOUT is reserved for SARIF/JSON).
Progress, timing, and execution diagnostics are written to STDERR.
Optionally, use `--log-file <path>` and `--log-format json` for CI-friendly structured logs.

## Quick start (planned)
```bash
# SARIF to STDOUT, logs to STDERR
roslyntic check .
# or
roslyntic check path/to/MySolution.sln --format sarif > results.sarif

# Keep logs in a file (CI)
roslyntic check path/to/MySolution.sln --format sarif --log-file roslyntic.log --log-format json > results.sarif
```

## Rules

### Built-in rules (initial MVP)
- AGARCH0001 Layer violation (e.g., UI → Infrastructure/DB direct access)
- AGCOMP0001 Cyclomatic complexity threshold exceeded
- AGARCH0002 Dependency cycle detection (Phase 2)

### Agent rules (Mentorship-as-Code)

Rules can live under .agent-rules/ and will be loaded by the CLI.

Important: for safety, rules may run in an isolated worker process with a timeout.

## Safety model

To avoid infinite loops and resource abuse from agent-generated rules, Roslyntic can run rule evaluation inside a worker process and enforce:
•	timeout
•	crash isolation
•	(optional) memory limits

If a plugin fails or times out, Roslyntic reports a deterministic diagnostic (e.g., AGSAFE9001) instead of crashing.

## CI integration

Roslyntic is designed to run in GitHub Actions and publish SARIF to Code Scanning.

## Status

This is an OSS project under active development. See AGENTS.md for development rules and agent workflow.

## License
MIT License. See [LICENSE](LICENSE) for details.
