# Copilot instructions (Roslyntic)

Follow `AGENTS.md` strictly.

Befo[copilot-instructions.md](copilot-instructions.md)re writing code, read:
1) `docs/agent-playbook.md`
2) `docs/architecture.md`
3) `docs/rules.md`

Hard rules:
- No network calls.
- STDOUT must be SARIF/JSON only.
- Deterministic ordering & stable rule IDs.
- Before implementing changes that affect contracts/safety/SARIF, check `docs/adr/` and update/add an ADR if needed.