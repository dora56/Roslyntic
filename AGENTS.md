# AGENTS.md (Roslyntic)

This repository is built with AI coding agents (GitHub Copilot) in mind.

## Non-negotiable contracts
- Deterministic results only (no probabilistic review content).
- Local-only execution: **no network calls** and no telemetry by default.
- **STDOUT is machine-readable only** (SARIF 2.1.0 or JSON). No decoration, no ANSI.
- **Observability must not pollute STDOUT** (use STDERR/log file; see docs).
- **STDERR is for humans** (execution errors, diagnostics).
- Output must be deterministic across runs (stable ordering & stable rule IDs).
- NativeAOT is **out of scope**.

## Read these first
- `docs/agent-playbook.md` (how to implement & validate)
- `docs/architecture.md` (project structure and responsibilities)
- `docs/rules.md` (rules, IDs, SARIF mapping)