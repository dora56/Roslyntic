# ADR-0002: SARIF Output Strategy
Status: Accepted
Date: 2026-03-05

## Context
Static analysis results need a standardized, machine-readable format for CI and code scanning.
SARIF 2.1.0 is the industry standard with broad tooling support.
The full SARIF spec is large; a minimal payload improves agent parsing speed.
Non-deterministic SARIF output breaks baselines and diff workflows.

## Decision
- Adopt SARIF 2.1.0 as the default output format (JSON via --format json).
- Minimal required fields:
  - version: "2.1.0"
  - runs[].tool.driver (name, version, informationUri)
  - runs[].tool.driver.rules[] (id, shortDescription, helpUri)
  - runs[].results[] (ruleId, level, message, locations with region)
- Deterministic payload:
  - Sort results per ADR-0001 (path -> line -> column -> ruleId)
  - Sort rules by id (lexicographic)
  - Stable JSON property ordering
  - Omit timestamps and run GUIDs by default
- GitHub Code Scanning compatibility:
  - Provide helpUri for each rule when available
  - Use region with startLine, startColumn, endLine, endColumn (1-based)
  - Map internal levels to error, warning, note
- No embedded source snippets in MVP.

## Consequences
- Easier: direct upload to GitHub Code Scanning.
- Easier: small, predictable JSON for agents and snapshots.
- Harder: custom serialization to enforce ordering and minimal payload.
- Risk: consumers may require extra fields in the future.

## Alternatives considered
- Custom JSON only (rejected: no ecosystem tooling support).
- Full SARIF with optional fields (rejected: payload bloat and unstable output).
- XML or CSV (rejected: poor structure for locations).
- Include source snippets by default (rejected: leaks code and increases size).

## Notes
- Deterministic ordering is defined in ADR-0001.
- SARIF writer implementation will live in Roslyntic.Sarif.
- Reference: https://docs.oasis-open.org/sarif/sarif/v2.1.0/sarif-v2.1.0.html
