# Rules & SARIF mapping — Roslyntic

## ADR cross-references
- [ADR-0001: Output Contract](adr/ADR-0001-output-contract.md) — STDOUT/STDERR separation, deterministic ordering, exit codes
- [ADR-0002: SARIF Strategy](adr/ADR-0002-sarif-strategy.md) — SARIF 2.1.0 adoption, minimal fields, GitHub compatibility
- [ADR-0003: Plugin Safety Model](adr/ADR-0003-plugin-safety-model.md) — Phase 2+ isolated worker process, no in-process untrusted plugins

## Rule ID scheme
- Architecture: `AGARCH####`
- Complexity: `AGCOMP####`
- Safety/runtime: `AGSAFE####`

Rule IDs MUST remain stable across releases.

## Built-in rules (MVP)
### AGARCH0001 — Layer violation
Detect forbidden dependencies between layers (UI/Application/Domain/Infrastructure) using Roslyn semantic analysis.

#### Default layer inference (MVP)
Layer is inferred from project name or root namespace containing:
- `*.UI` => UI
- `*.Application` => Application
- `*.Domain` => Domain
- `*.Infrastructure` => Infrastructure

#### Default forbidden edges
- UI -> Infrastructure
- UI -> Domain
- Domain -> Infrastructure

#### Reporting requirements
Each diagnostic should include:
- Location: the syntax node that introduces the dependency (e.g., type usage / invocation)
- Properties:
    - `fromLayer`, `toLayer`
    - `fromSymbol`, `toSymbol` (or similar)
    - `dependencyKind` (reference/creation/invocation/inheritance/attribute)

### AGCOMP0001 — Cyclomatic complexity threshold exceeded
Compute cyclomatic complexity per method and report if above threshold.

#### Default threshold
- 15

#### Count constructs (MVP)
Count typical branching constructs:
- `if`, `else if`
- `switch` / `case` labels
- loops: `for`, `foreach`, `while`, `do`
- `catch`
- conditional operator `?:`
- boolean operators `&&` and `||`

#### Reporting requirements
- Location: method declaration identifier region
- Properties:
    - `methodName`
    - `complexity`
    - `threshold`

## Phase 2 rules
### AGARCH0002 — Dependency cycle detection
Detect cycles using SCC on a dependency graph (namespace/class/project node selection to be decided).

### AGSAFE9001 — Plugin execution timeout (Phase 2)
If plugin execution exceeds timeout, report a deterministic finding instead of crashing.

## SARIF 2.1.0 mapping rules
### General
- SARIF output must be deterministic and minimal.
- STDOUT is SARIF/JSON only.

### Tool metadata
- `runs[0].tool.driver.name = "Roslyntic"`
- `runs[0].tool.driver.version` must be set
- `rules[]` must include:
    - `id`
    - `shortDescription.text`
    - `fullDescription.text`
    - `helpUri` (if available)
    - `properties.category`

### Results
Each result must include:
- `ruleId`
- `level` (error/warning/note)
- `message.text` (template-based)
- `locations[0].physicalLocation`:
    - `artifactLocation.uri` (file path)
    - `region.startLine`, `startColumn` (and end if available)
- Optional but recommended:
    - `properties` for structured fields listed above
    - `partialFingerprints` if a stable fingerprint is available

### Ordering
Before writing SARIF, sort results:
1) artifactLocation.uri
2) region.startLine
3) region.startColumn
4) ruleId