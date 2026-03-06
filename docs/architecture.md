# Architecture — Roslyntic

This document describes the high-level architecture and responsibilities.

## Core idea
Roslyntic produces deterministic static analysis findings derived from Roslyn semantics and emits them as SARIF/JSON for AI agents and CI systems.

## Key ADRs
Architecture decisions that define non-negotiable contracts:
- [ADR-0001: Output Contract](adr/ADR-0001-output-contract.md) — STDOUT/STDERR separation, deterministic ordering, exit codes
- [ADR-0002: SARIF Strategy](adr/ADR-0002-sarif-strategy.md) — SARIF 2.1.0 adoption, minimal fields, GitHub compatibility
- [ADR-0003: Plugin Safety Model](adr/ADR-0003-plugin-safety-model.md) — Phase 2+ isolated worker process, no in-process untrusted plugins

## Components
### src/Roslyntic.Cli/Program.cs
- CLI entrypoint that delegates to `Core/RoslynticApp.RunAsync`.

### src/Roslyntic.Cli/Core
- Parses args (`roslyntic check ...`)
- Selects output format
- Writes machine output to STDOUT, human messages to STDERR
- Handles exit codes
- Controls observability options (`--log-file`, `--log-format`, verbosity)
- Loads `.sln`/`.slnx`/`.csproj` via MSBuildWorkspace
- Orchestrates analysis pipeline:
    - build analysis context
    - run rules
    - sort diagnostics
    - format output
- Emits structured observability events to a logger abstraction (sink = STDERR/log file)

### src/Roslyntic.Cli/Analysis
- Provides reusable analysis utilities:
    - dependency extraction helpers (semantic model based)
    - cyclomatic complexity calculator
    - (future) dependency graph + SCC cycle detection
- Should not write to STDOUT/STDERR directly (use logger abstraction if needed)

### src/Roslyntic.Cli/Rules
- Implements built-in rules using Analysis utilities
- Outputs diagnostics in an internal, format-agnostic model
- Should not write to STDOUT/STDERR directly

### src/Roslyntic.Cli/Sarif
- Maps internal diagnostics to SARIF 2.1.0:
    - tool metadata
    - rules definitions
    - results with locations/regions
- Keeps payload small and deterministic
- Should not write logs to STDOUT (only the SARIF writer writes to STDOUT)

### tests/Roslyntic.Tests
- Unit tests for analysis utilities and rule behavior
- Snapshot tests for SARIF output
- Integration tests using `sample/`

## Observability model (NFR)
Roslyntic must be observable without polluting STDOUT.

### Minimum signals
- durations: total + phase durations (workspace load / rule execution / output write)
- counts: projects/documents/rules executed/findings by level
- failures: categorized reason

### Output
- Default: short text logs to STDERR
- Optional: newline-delimited JSON logs to STDERR and/or file via `--log-file`
- No telemetry/network by default

## Internal models (suggested)
### Diagnostic (internal)
- `RuleId` (string)
- `Level` (error/warning/note)
- `Message` (string, template-based)
- `Location` (file + region)
- `Properties` (dictionary for structured data)
- Optional: `Fingerprint` for deduplication

### Rule metadata (internal)
- `Id`, `Title`, `Description`, `Category`, `DefaultLevel`, `HelpUri`

## Determinism rules
- Sort diagnostics: path → line → column → ruleId
- Keep messages template-based
- Avoid any non-deterministic enumeration order (use ordered collections)

## Out of scope (v1)
- NativeAOT
- In-process untrusted plugin execution
- Fully incremental analysis across git diffs (can be added later)
