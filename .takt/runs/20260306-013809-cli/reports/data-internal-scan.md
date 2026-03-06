# 内部コードスキャン＋ドキュメント確認 レポート

取得日: 2026-03-06

---

## 1. OTel 関連コード・依存の有無

### 1-1. ソースコード（`**/*.cs`）
検索キーワード: `OpenTelemetry`, `ActivitySource`, `OTLP`, `Meter`, `Tracer`

**検出なし**

- 対象ファイル数: 1 件（`Roslyntic.Cli/Program.cs`）
- 内容: `Console.WriteLine("Hello, World!");` のみのスケルトン実装
- 出典: `Roslyntic.Cli/Program.cs`

### 1-2. プロジェクトファイル（`**/*.csproj`）
検索キーワード: 同上

**検出なし**

- 対象ファイル数: 1 件（`Roslyntic.Cli/Roslyntic.Cli.csproj`）
- `PackageReference` ゼロ（`PropertyGroup` のみ）
- 出典: `Roslyntic.Cli/Roslyntic.Cli.csproj`

---

## 2. ネットワーク通信コードの有無

### 2-1. ソースコード（`**/*.cs`）
検索キーワード: `HttpClient`, `WebClient`, `WebRequest`, `TcpClient`, `HttpClientFactory`, `SocketsHttpHandler`

**検出なし**

- 出典: `Roslyntic.Cli/Program.cs`（全 3 行のスケルトン）

### 2-2. プロジェクトファイル（`**/*.csproj`）
**検出なし**

---

## 3. ADR-0001 が定める「no network」スコープの明確化

出典: `docs/adr/ADR-0001-output-contract.md`（2026-03-05 Accepted）

### ADR-0001 の該当箇所
> Local-only execution: no network calls, no telemetry by default.

### ビルド時 vs ランタイムの分類

| スコープ | 定義 | 備考 |
|---|---|---|
| **ランタイム** | ネットワーク呼び出し禁止（デフォルト） | ADR-0001 明示。`HttpClient` 等の使用不可 |
| **テレメトリ** | デフォルト無効 | `docs/observability.md` §NF5 "No network/telemetry by default" で同様に確認 |
| **ビルド時** | ADR-0001 では明示的に言及なし | `MSBuildWorkspace` はローカル MSBuild を呼び出すが、ネットワーク I/O は行わない想定（NuGet restore 等は別途制御が必要） |

### 補足: MSBuildWorkspace の扱い
`docs/architecture.md` では `Roslyntic.Core` が `MSBuildWorkspace` でソリューションを読み込む設計。`MSBuildWorkspace` は NuGet パッケージキャッシュを参照するがネットワーク通信はトリガーしない（`--no-restore` 相当の運用が前提）。NuGet restore のランタイム実行については ADR-0001 のスコープが **不明確** — 後続 ADR または README での明文化が推奨される。

---

## 4. 現行 NDJSON ログ仕様（NF5.2 / NF5.3）の構造・フィールド定義

出典: `docs/observability.md`（NF5.2, NF5.3 節）

### NF5.2 必須シグナル（STDERR またはログファイルへ出力）

| カテゴリ | フィールド |
|---|---|
| Timing | total duration, workspace load, rule execution, output write（フェーズ別） |
| Scale | projects count, documents count |
| Quality signals | rules executed count, findings count（error / warning / note 別） |
| Failure classification | workspace load failure, rule execution failure, unexpected exception, (将来) plugin timeout / plugin crash |

### NF5.3 構造化ログ（`--log-format json` 時）
形式: NDJSON（1 行 1 JSON オブジェクト）、キーはバージョン間で安定

| フィールド | 型 | 説明 |
|---|---|---|
| `event` | string | `run_start` \| `phase_end` \| `run_end` \| `error` |
| `runId` | string | 実行識別子 |
| `toolVersion` | string | ツールバージョン |
| `command` | string | 実行コマンド文字列 |
| `path` | string | 解析対象パス |
| `format` | string | `sarif` \| `json` |
| `exitCode` | number | 0 \| 1 \| 2 |
| `durMs` | number | 経過時間（ms） |
| `phase` | string | `workspace_load` \| `rules` \| `output_write` |
| `counts` | object | `{ projects, documents, rulesExecuted, findingsError, findingsWarning, findingsNote }` |
| `error` | object | `{ category, message }`（message は短く非機密） |

---

## 5. 全 PackageReference の一覧

### `Roslyntic.Cli/Roslyntic.Cli.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

**PackageReference: 0 件（未定義）**

### 確認済み csproj ファイル一覧

| ファイルパス | PackageReference 件数 |
|---|---|
| `Roslyntic.Cli/Roslyntic.Cli.csproj` | 0 |

### 備考
`docs/architecture.md` によると `Roslyntic.Core`, `Roslyntic.Analysis`, `Roslyntic.Rules`, `Roslyntic.Sarif`, `Roslyntic.Tests` の各プロジェクトが設計上存在するが、対応する `.csproj` は現時点でリポジトリに存在しない（未作成のスケルトン段階）。

---

## 6. 調査できなかった項目とその理由

| 項目 | 理由 |
|---|---|
| `Roslyntic.Core` / `Roslyntic.Analysis` / `Roslyntic.Rules` / `Roslyntic.Sarif` / `Roslyntic.Tests` の PackageReference | 対応する `.csproj` ファイルがリポジトリに存在しない（スケルトン未作成段階） |
| `MSBuildWorkspace` のビルド時ネットワーク有無（NuGet restore 挙動） | ADR-0001 はランタイムのみ明示。ビルド時の NuGet restore に関する明文的なポリシー記述が docs に存在しない |
| OTel / HttpClient の将来的な導入計画 | 設計ドキュメント（ADR-0001〜0003、observability.md）には記載なし。導入する場合は新規 ADR が必要 |

---

## 付録: 参照ドキュメント一覧

| ドキュメント | パス |
|---|---|
| CLAUDE.md | `CLAUDE.md` |
| ADR-0001 | `docs/adr/ADR-0001-output-contract.md` |
| ADR-0002 | `docs/adr/ADR-0002-sarif-strategy.md`（今回精読外） |
| ADR-0003 | `docs/adr/ADR-0003-plugin-safety-model.md`（今回精読外） |
| architecture.md | `docs/architecture.md` |
| observability.md | `docs/observability.md` |
| rules.md | `docs/rules.md`（今回精読外） |
| agent-playbook.md | `docs/agent-playbook.md`（今回精読外） |
