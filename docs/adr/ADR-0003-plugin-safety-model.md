# ADR-0003: Plugin Safety Model (Phase 2+)
Status: Accepted
Date: 2026-03-05

## Context
Roslyntic aims to support agent-generated C# rule plugins.
Untrusted plugins can hang, crash, or consume excessive resources.
In-process execution exposes the host CLI to non-deterministic failures.
CI systems require bounded execution time and deterministic failure reporting.

## Decision
- Phase 1 (MVP): no plugin support; built-in rules only.
- Phase 2+:
  - Run plugins in an isolated worker process (no in-process execution).
  - Enforce timeouts; kill the worker if execution exceeds the limit.
  - Report deterministic failures:
    - AGSAFE9001 for plugin timeout
    - AGSAFE9002 for plugin crash
    - Include plugin name, phase, and timeout duration in the message
  - Exit with code 2 if any plugin fails to load.
- Explicitly disallow:
  - In-process Assembly.LoadFrom for untrusted plugins
  - Dynamic compilation of untrusted source without isolation
  - Reflection-based discovery without validation

## Consequences
- Easier: host stability; plugin failures cannot crash the CLI.
- Easier: CI reliability with bounded execution time.
- Harder: IPC and process management overhead.
- Harder: plugin debugging requires attaching to the worker process.
- Risk: restrictive environments may block worker process creation.

## Alternatives considered
- In-process plugins with AppDomain isolation (rejected: deprecated in .NET Core).
- In-process plugins with CancellationToken (rejected: cooperative cancellation is unreliable).
- WASM sandbox execution (deferred: high complexity for Phase 2).
- No plugin support (rejected: limits mentorship-as-code vision).

## Notes
- Built-in rules in Phase 1: AGARCH0001, AGCOMP0001.
- AGSAFE9001 and AGSAFE9002 are reserved for plugin safety diagnostics.
- Phase 2 details need a separate ADR for IPC and plugin contract.
