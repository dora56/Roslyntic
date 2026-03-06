## decomposition

---

{
  "parts": [
    {
      "id": "internal-scan",
      "title": "内部コードスキャン＋ドキュメント確認",
      "instruction": "## 調査タスク: Roslyntic 内部コードスキャン＋ドキュメント確認\n\n### 作業ディレクトリ\n`/Users/r-hanaoka/Development/src/github.com/dora56/takt-worktrees/20260306T0214-cliniokeruobuzaababiriteitonet`\n\n### 調査手順\n\n#### Step 1: コードスキャン（Grep）\n以下のキーワードで `**/*.cs` と `**/*.csproj` を網羅的に検索する:\n- OTel 関連: `OpenTelemetry`, `ActivitySource`, `OTLP`, `Meter`, `Tracer`\n- ネットワーク通信: `HttpClient`, `WebClient`, `WebRequest`, `TcpClient`, `HttpClientFactory`, `SocketsHttpHandler`\n- NuGet/MSBuild: `NuGetRestore`, `MSBuildWorkspace`, `PackageReference`\n\n#### Step 2: ドキュメント全文読み取り\n以下のファイルを全文読み取る:\n- `docs/observability.md`（NF5.1〜NF5.3 の定義）\n- `docs/architecture.md`（コンポーネント構成）\n- `docs/adr/ADR-0001-output-contract.md`（「no network / no telemetry」制約の定義）\n- `CLAUDE.md`（非交渉の制約一覧）\n\n#### Step 3: csproj の依存確認\n`**/*.csproj` を全て読み取り、PackageReference の全リストを抽出する。\n\n### データ保存先\n`.takt/runs/20260306-013809-cli/reports/data-internal-scan.md` に以下の構成で保存:\n1. OTel 関連コード・依存の有無（ファイル・行番号付き一覧 or「検出なし」）\n2. ネットワーク通信コードの有無（同上）\n3. ADR-0001 が定める「no network」スコープの明確化（ビルド時 vs ランタイム）\n4. 現行 NDJSON ログ仕様（NF5.2/5.3）の構造・フィールド定義\n5. 全 PackageReference の一覧\n6. 調査できなかった項目とその理由\n- 各項目に出典ファイルパスを含める\n- 取得日を記録する"
    },
    {
      "id": "otel-patterns",
      "title": "OpenTelemetry × CLI ツール採用パターン調査",
      "instruction": "## 調査タスク: OpenTelemetry × CLI ツール採用パターン外部調査\n\n### 作業ディレクトリ\n`/Users/r-hanaoka/Development/src/github.com/dora56/takt-worktrees/20260306T0214-cliniokeruobuzaababiriteitonet`\n\n### 調査手順\n\n#### Step 1: CLI/短寿命プロセスにおける OTel 採用 OSS 事例\n以下のプロジェクトについて GitHub / 公式ドキュメントから採用パターンを調査:\n- `dotnet-monitor`（microsoft/dotnet-monitor）\n- `dotnet-trace` / `dotnet-counters`（dotnet/diagnostics）\n- Trivy（aquasecurity/trivy）\n- Semgrep（semgrep/semgrep）\n- grype（anchore/grype）\n\n推奨検索クエリ:\n- `dotnet-monitor OpenTelemetry OTLP site:github.com/dotnet`\n- `trivy OpenTelemetry tracing site:github.com/aquasecurity`\n- `semgrep OpenTelemetry metrics CLI`\n\n#### Step 2: 短寿命プロセス特有の OTel 課題\n以下のテーマで調査:\n- バッチ export のフラッシュタイミング（`ForceFlush` / `Shutdown`）\n- コールドスタートコスト、SDK 初期化コスト\n- 推奨: `OpenTelemetry \"short-lived\" OR \"short lived\" process flush CLI`\n\n#### Step 3: .NET OTel パッケージサイズ・依存関係\n- NuGet.org で以下を確認:\n  - `OpenTelemetry`\n  - `OpenTelemetry.Exporter.OpenTelemetryProtocol`\n  - `OpenTelemetry.Extensions.Hosting`\n- サイズ（MB）、依存パッケージ数を記録する\n- URL: https://www.nuget.org/packages/OpenTelemetry / https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol\n\n#### Step 4: opt-in OTel export 実装パターン\n- `--otel-endpoint` / `--otel-export` フラグ等の CLI ツールでの実装パターンを調査\n- 環境変数 `OTEL_EXPORTER_OTLP_ENDPOINT` との組み合わせ事例\n\n### データ保存先\n`.takt/runs/20260306-013809-cli/reports/data-otel-patterns.md` に以下の構成で保存:\n1. 採用 OSS 事例のサマリー表（プロジェクト名・採用形態・出典URL）\n2. 短寿命プロセスにおける OTel 技術的課題と対策\n3. .NET OTel SDK のパッケージサイズ・依存数（数値付き）\n4. opt-in 実装パターンの代表例\n5. Roslyntic への適用可能性の初期評価（分析）\n6. 調査できなかった項目とその理由\n- 全項目に出典URL・取得日を明記\n- 数値には出典を必ず付ける"
    },
    {
      "id": "network-policy",
      "title": "類似ツールのネットワークポリシー比較調査",
      "instruction": "## 調査タスク: 類似静的解析CLIツールのネットワークポリシー比較\n\n### 作業ディレクトリ\n`/Users/r-hanaoka/Development/src/github.com/dora56/takt-worktrees/20260306T0214-cliniokeruobuzaababiriteitonet`\n\n### 調査手順\n\n#### Step 1: 各ツールのネットワークポリシー調査\n以下のツールについて公式ドキュメント・GitHub から調査:\n\n**A. Roslyn Analyzers (Microsoft.CodeAnalysis)**\n- ランタイム中のネットワーク通信の有無\n- 検索: `Microsoft.CodeAnalysis \"no network\" OR \"offline\" runtime site:github.com`\n\n**B. StyleCop.Analyzers**\n- オフライン動作の可否、ネットワーク依存の有無\n- GitHub: https://github.com/DotNetAnalyzers/StyleCopAnalyzers\n\n**C. SonarScanner for .NET**\n- オフラインモード / エアギャップ対応の仕様\n- 検索: `SonarScanner .NET \"offline mode\" OR \"air-gap\" documentation`\n- 公式Docs: https://docs.sonarsource.com/sonarqube/latest/analyzing-source-code/scanners/sonarscanner-for-dotnet/\n\n**D. Semgrep**\n- `--offline` フラグ等のネットワークポリシー\n- 検索: `Semgrep \"--offline\" network policy site:semgrep.dev OR site:github.com/semgrep`\n\n**E. MSBuildWorkspace の暗黙的ネットワーク通信**\n- ソリューションロード時の NuGet 復元の有無\n- 検索: `MSBuildWorkspace NuGet restore network \"no network\" isolation`\n\n#### Step 2: エアギャップ / オフライン環境対応プラクティス\n- アップデートチェックの opt-in 化の事例\n- ライセンス認証のオフライン対応パターン\n- 検索: `static analysis CLI tool \"no network\" \"offline\" \"air-gap\" best practices`\n\n#### Step 3: 「no network calls」制約の業界標準解釈\n- ビルド時（NuGet restore）vs ランタイム時の区別\n- CI/CD 環境での「no network」の一般的定義\n\n### データ保存先\n`.takt/runs/20260306-013809-cli/reports/data-network-policy.md` に以下の構成で保存:\n1. ツール別ネットワークポリシー比較表（ツール名・ポリシー種別・オフライン対応・出典URL）\n2. エアギャップ対応プラクティスの整理\n3. 「no network calls」のスコープ解釈（業界標準との照合）\n4. MSBuildWorkspace のネットワーク動作に関する情報\n5. Roslyntic の制約への示唆（分析）\n6. 調査できなかった項目とその理由\n- 全項目に出典URL・取得日を明記\n- 数値・仕様には必ず出典を付ける"
    }
  ]
}

---

## internal-scan: 内部コードスキャン＋ドキュメント確認
レポートを保存しました。以下に調査結果のサマリーを示します。

---

## 調査結果サマリー

### 1. OTel 関連コード・依存 → **検出なし**
`.cs` / `.csproj` 全ファイルを対象に `OpenTelemetry`, `ActivitySource`, `OTLP`, `Meter`, `Tracer` を検索。ヒットゼロ。現時点でコードベースは `Program.cs`（3行スケルトン）と `Roslyntic.Cli.csproj`（PackageReference なし）のみ。

### 2. ネットワーク通信コード → **検出なし**
`HttpClient`, `WebClient`, `WebRequest`, `TcpClient`, `HttpClientFactory`, `SocketsHttpHandler` いずれも未使用。コードが実質スケルトンのため当然の結果。

### 3. ADR-0001「no network」スコープの明確化
| スコープ | 結論 |
|---|---|
| **ランタイム** | 明示的に禁止（"no network calls, no telemetry by default"） |
| **テレメトリ** | デフォルト無効（`observability.md` でも再確認） |
| **ビルド時（NuGet restore 等）** | ADR-0001 では未言及。`MSBuildWorkspace` はローカル呼び出しのみ想定だが、明文的なポリシー記述が**欠如**している |

### 4. NDJSON ログ仕様（NF5.2/5.3）
`--log-format json` 時に NDJSON 出力。主要フィールドは `event`, `runId`, `toolVersion`, `durMs`, `phase`, `counts{projects, documents, rulesExecuted, findingsError/Warning/Note}`, `error{category, message}`。ソースコードへの実装はまだなし（ドキュメント仕様のみ）。

### 5. 全 PackageReference
**0 件**。`Roslyntic.Cli/Roslyntic.Cli.csproj` に `PackageReference` なし。他プロジェクト（Core / Analysis / Rules / Sarif / Tests）の `.csproj` 自体が未作成。

### 調査不可項目
- `Roslyntic.Core` 等の PackageReference → `.csproj` が存在しないため
- ビルド時 NuGet restore のネットワーク有無 → ADR/ドキュメントに明記なし

---

## network-policy: 類似ツールのネットワークポリシー比較調査
調査が完了しました。レポートを `.takt/runs/20260306-013809-cli/reports/data-network-policy.md` に保存しました。

---

## 調査結果サマリー

### 主要な発見

**ツール別ネットワークポリシー（結論）**

| ツール | ランタイム通信 | オフライン可否 |
|---|---|---|
| Roslyn Analyzers | **なし**（設計上） | ◎ 完全可 |
| StyleCop.Analyzers | **なし** | ◎ 完全可 |
| SonarScanner for .NET | **常時必須**（begin/end がサーバー接続前提） | ✗ 不可 |
| Semgrep | デフォルトあり（バージョンチェック・メトリクス） | △ フラグ組み合わせで可 |
| MSBuildWorkspace | **条件次第**（未キャッシュパッケージがある場合のみ） | △ 事前 restore 済みなら可 |

### Roslyntic への示唆

1. **Roslyntic の「no network calls」制約は実現可能** — Roslyn/MSBuildWorkspace スタックは設計上ネットワーク不要。NuGet が事前キャッシュ済みであればランタイム通信は発生しない。

2. **「no network」のスコープ明確化が必要** — 業界標準では「ビルド前の `dotnet restore`（=ネットワーク許容）」と「分析ランタイム（=ネットワーク禁止）」を区別する。Roslyntic のドキュメント / READMEで「事前に `dotnet restore` を実行済みの環境を前提とする」と明記することを推奨。

3. **SonarScanner との差別化ポイント** — SonarScanner はサーバー接続必須のため、エアギャップ CI では使えない。Roslyntic のローカル完結型 SARIF 出力は競争優位になり得る。

4. **テレメトリはデフォルト OFF** — Semgrep/SonarScanner がデフォルト ON でユーザーを悩ませている中、Roslyntic の「no telemetry by default」は明確な差別化。将来追加する場合も環境変数 opt-in のみ。

Sources:
- [GitHub - DotNetAnalyzers/StyleCopAnalyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)
- [Roslyn issue #29525](https://github.com/dotnet/roslyn/issues/29525)
- [Semgrep CLI Reference](https://semgrep.dev/docs/cli-reference)
- [Offline execution issue #8793 - semgrep/semgrep](https://github.com/semgrep/semgrep/issues/8793)
- [Offline .NET Sonar scanner installation - Sonar Community](https://community.sonarsource.com/t/offline-net-sonar-scanner-installation/126631)
- [NuGet Package Restore - Microsoft Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore)
- [Using MSBuildWorkspace - Gist by DustinCampbell](https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3)
- [.NET SDK telemetry - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/telemetry)
- [6 telemetry best practices for CLI tools - marcon.me](https://marcon.me/articles/cli-telemetry-best-practices/)

---

## otel-patterns: OpenTelemetry × CLI ツール採用パターン調査
調査完了。レポートを保存しました。以下に調査結果のサマリーを報告します。

---

## 調査結果報告

### Step 1: CLI/短寿命プロセスにおける OTel 採用 OSS 事例

#### 実際に OTel を採用しているプロジェクト

| プロジェクト | 採用形態 | シグナル |
|---|---|---|
| **Semgrep** | `--trace` フラグによる opt-in。OTel traces を OTLP で送信 | Traces のみ |
| **Docker CLI** | `DOCKER_CLI_OTEL_EXPORTER_OTLP_ENDPOINT` 環境変数による opt-in | Metrics のみ (`command.time`) |
| **otel-cli** | shell script 用ラッパー。`OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時は non-recording mode | Traces |

#### OTel を採用していないプロジェクト（想定と異なった）

- **dotnet-monitor**: OTel 非採用。Prometheus 形式 `/metrics` エンドポイントのみ
- **dotnet-trace / dotnet-counters**: OTel の「消費者」ツールであり、自身は OTel SDK 未使用
- **Trivy**: 独自の匿名使用統計のみ（OTel 非採用）
- **grype**: OTel 実装なし（確認できなかった）

### Step 2: 短寿命プロセス特有の OTel 課題

**最重要課題**: バッチエクスポートのフラッシュ未完了

- `PeriodicExportingMetricReader` のデフォルト間隔は 60 秒 → 短命プロセスでは metrics が全く出ない
- **対策**: `IHost` を `using` で確実に Dispose するか、`TracerProvider.Shutdown()` を明示呼び出し
- Semgrep も「OTel 自身のトレースを自分自身が送信してしまう」問題に遭遇し、self-trace を無効化
- otel-cli の推奨: localhost に OTel Collector を立て接続コストを最小化

### Step 3: .NET OTel パッケージサイズ（2026-03-06 取得）

| パッケージ | バージョン | サイズ | 依存数 |
|---|---|---|---|
| `OpenTelemetry` | 1.15.0 | 782.97 KB | 3 |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.15.0 | 489.93 KB | 1（.NET 9/10） |
| `OpenTelemetry.Extensions.Hosting` | 1.15.0 | 89.4 KB | 2 |
| **合計** | — | **≒ 1.36 MB** | — |

### Step 4: opt-in 実装パターン

業界標準は **「`OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時は完全スキップ」** パターン。Docker CLI・otel-cli ともにこの設計。Semgrep のみ CLI フラグ (`--trace`) を追加採用。

### Roslyntic への適用可能性

- **採用推奨**: ✅ 条件付き採用可能（STDOUT 汚染なし、NativeAOT 対象外でリスク低）
- **必須要件**: `OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時のゼロコスト・ゼロネットワーク保証
- **実装工数**: ≒ 15h（基本 span 追加含む）
- **注意点**: `using var host = ...` による Dispose パターン必須（フラッシュ保証のため）

レポート保存先: `.takt/runs/20260306-013809-cli/reports/data-otel-patterns.md`