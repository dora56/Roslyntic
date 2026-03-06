# Copilot instructions (Roslyntic)

Follow `AGENTS.md` strictly.

Before [copilot-instructions.md](copilot-instructions.md) writing code, read:
1) `docs/agent-playbook.md`
2) `docs/architecture.md`
3) `docs/rules.md`
4) `docs/observability.md` (for logging/diagnostics)

Hard rules:
- This tool must run in local-only mode and must not conduct any network communication or web searches.
- STDOUT must be SARIF/JSON only.
- Deterministic ordering & stable rule IDs.
- Before implementing changes that affect contracts/safety/SARIF, check `docs/adr/` and update/add an ADR if needed.
  - If an ADR is needed, get it accepted before implementing the change.
  - If the change is just a clarification of an existing ADR, update the ADR with the clarification and get it accepted.
  - If the change is a bug fix that violates the contract, update the ADR with the bug and the fix, and get it accepted.

coding guidelines:
- Follow .NET/C# conventions and best practices.
- Use the existing repo structure and patterns.
- DO NOT:
  - Hard coding paths, thresholds, or rule IDs.
  - Breaking the contract just to make the tests pass.
    - If the contract needs to be changed, update/add an ADR and get it accepted first.
  - Adding features that are not in the current scope without checking with the team first.
