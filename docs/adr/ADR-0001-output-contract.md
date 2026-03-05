# ADR-0001: Output Contract (STDOUT/STDERR, Exit Codes, Determinism)
Status: Accepted
Date: 2026-03-05

## Context
Roslyntic targets AI agents and CI systems that parse machine output.
Mixing human output into STDOUT breaks parsers and automation.
Non-deterministic ordering causes baseline diffs and flaky agent feedback.
Exit codes must clearly distinguish findings from tool failures.

## Decision
- STDOUT is machine-readable only: SARIF 2.1.0 or JSON. No decoration or ANSI.
- STDERR is for humans: progress, warnings, execution errors, timing, observability logs.
- Deterministic ordering for all diagnostics:
  1) file path (ordinal string comparison)
  2) start line
  3) start column
  4) ruleId
- Exit codes:
  - 0: no findings above threshold
  - 1: findings exist (analysis succeeded)
  - 2: tool execution failure
- Local-only execution: no network calls, no telemetry by default.

## Consequences
- Easier: CI gating and agent parsing with stable, machine-only STDOUT.
- Easier: deterministic output enables reliable diffs and baselines.
- Harder: output layer must buffer results to sort deterministically.
- Harder: internal components must log via a logger abstraction, not STDOUT.
- Risk: culture-dependent sorting must be avoided (use ordinal comparisons).

## Alternatives considered
- Mixed output on STDOUT (rejected: breaks parsers and automation).
- Exit code 1 for tool failures (rejected: ambiguous in CI).
- Non-deterministic ordering (rejected: breaks baselines and diffs).
- Telemetry enabled by default (rejected: violates local-only privacy).

## Notes
- Observability details: docs/observability.md
- SARIF ordering requirements are referenced in ADR-0002.
