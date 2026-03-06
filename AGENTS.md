# AGENTS.md (Roslyntic)

This repository is built with AI coding agents (GitHub Copilot) in mind.

## Non-negotiable contracts
- Deterministic results only (no probabilistic review content).
- Local-only execution: **no network calls** and no telemetry by default.
  - If investigation is needed, you may conduct web searches or retrieve information from the web, but the tool itself must not perform any network communication.
- **STDOUT is machine-readable only** (SARIF 2.1.0 or JSON). No decoration, no ANSI.
- **Observability must not pollute STDOUT** (use STDERR/log file; see docs).
- **STDERR is for humans** (execution errors, diagnostics).
- Output must be deterministic across runs (stable ordering & stable rule IDs).
- NativeAOT is **out of scope**.

## Read these first
- `docs/agent-playbook.md` (how to implement & validate)
- `docs/architecture.md` (project structure and responsibilities)
- `docs/rules.md` (rules, IDs, SARIF mapping)

## Setup
1. Install .NET SDK 10.x.
2. Restore/build once:
   - `dotnet build Roslyntic.slnx`
3. Run CLI locally:
   - `dotnet run --project src/Roslyntic.Cli -- check <path>`

## Verify（PR前に通すべきコマンド）
- `dotnet build Roslyntic.slnx`
- `dotnet test Roslyntic.slnx`
- Determinism check (same input twice must produce identical STDOUT):
  - `dotnet run --project src/Roslyntic.Cli -- check <path> > /tmp/roslyntic-1.sarif`
  - `dotnet run --project src/Roslyntic.Cli -- check <path> > /tmp/roslyntic-2.sarif`
  - `diff -u /tmp/roslyntic-1.sarif /tmp/roslyntic-2.sarif`

## Repo-specific constraints
- 必ず使うツール:
  - `dotnet` CLI（build/test/run）
  - Roslyntic CLI（`check`）
- 絶対に触ってはいけないもの:
  - 既存ADRの契約（STDOUT/STDERR分離、終了コード、決定的順序、stable rule ID）を破る変更
  - Phase 2以降の未合意機能（未隔離プラグイン実行、NativeAOT導入）
- シークレット/本番環境の禁止事項:
  - シークレット（トークン/鍵/接続文字列）をコード・ログ・SARIFに出力しない
  - 本番システム/本番データへのアクセス処理を追加しない
  - ネットワーク呼び出し・テレメトリをデフォルト有効にしない

## Definition of Done
- `roslyntic check <path>` が `.sln` / `.slnx` / `.csproj` で動作する
- STDOUT は SARIF/JSON のみ、STDERR は人間向け情報のみ
- 終了コード `0/1/2` が契約通り
- 出力順序が決定的（path → line → column → ruleId）
- `AGARCH0001` / `AGCOMP0001` が安定 ruleId で報告される
- `dotnet build` / `dotnet test` が成功する

## Maintenance rule
- 開発進捗に応じて、`Setup` / `Verify` / `Repo-specific constraints` / `Definition of Done` の4章を継続更新すること。
