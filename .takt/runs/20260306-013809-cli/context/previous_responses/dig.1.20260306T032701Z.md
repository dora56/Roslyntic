## decomposition

---

{
  "parts": [
    {
      "id": "part-1-internal-files",
      "title": "ローカルファイル調査（既存ADR・docs・前回レポートの精読）",
      "instruction": "## 目的\n実装・ADR作成に必要な情報をプロジェクト内ファイルから収集する。\n\n## 作業ディレクトリ\n/Users/r-hanaoka/Development/src/github.com/dora56/takt-worktrees/20260306T0214-cliniokeruobuzaababiriteitonet\n\n## 調査対象ファイル（すべて読み込む）\n\n**ADR・ドキュメント:**\n- `docs/adr/ADR-0001-output-contract.md`\n- `docs/adr/ADR-0002-sarif-strategy.md`\n- `docs/adr/ADR-0003-plugin-safety-model.md`\n- `docs/observability.md`\n- `docs/architecture.md`\n\n**前回調査レポート:**\n- `.takt/runs/20260306-013809-cli/reports/research-report.md`\n- `.takt/runs/20260306-013809-cli/reports/analysis-1.md`\n- `.takt/runs/20260306-013809-cli/reports/data-otel-patterns.md`\n- `.takt/runs/20260306-013809-cli/reports/data-network-policy.md`\n- `.takt/runs/20260306-013809-cli/reports/data-internal-scan.md`\n\n**ソースコード:**\n- `Roslyntic.Cli/Roslyntic.Cli.csproj`\n- `Roslyntic.Cli/Program.cs`\n\n## 抽出すべき情報\n\n1. **ADRフォーマット**: 既存ADRのヘッダー構成（Status / Context / Decision / Consequences の書き方、Markdown見出しレベル）\n2. **「no network calls / no telemetry」の正確な文言**: ADR-0001のDecisionセクションから引用\n3. **ADR-0003プラグインとOTel**: プラグインワーカープロセスとのコンテキスト伝播で将来問題になりうる点\n4. **現行NDJSON仕様**: NF5.2/NF5.3の内容（OTel Logsで置き換えない理由の裏付け）\n5. **OTel実装コード**: `data-otel-patterns.md` §4 パターンD の完全なコードスニペット\n6. **MSBuildWorkspace NuGet制御**: 前回レポートに記載があれば `EnableNuGetPackageRestore=false` 等の正確なAPI\n7. **OTel SDKパッケージ**: バージョン・サイズ・起動コストのデータ\n8. **csproj現在状態**: 既存のPackageReferenceと `<Project Sdk>` の形式\n\n## データ保存ルール\n- 調査結果を `/Users/r-hanaoka/Development/src/github.com/dora56/takt-worktrees/20260306T0214-cliniokeruobuzaababiriteitonet/.takt/runs/20260306-013809-cli/reports/data-internal-state.md` に書き出す\n- 出典ファイル名・セクション名を明記する\n- 生データ（引用）を含める\n\n## 報告構成\n- ADRフォーマットのテンプレート（ヘッダー・セクション構成の実例）\n- 抽出した文言・コード・数値（出典ファイル名・セクションを明記）\n- OTel実装コードスニペット（パターンD）\n- MSBuildWorkspace NuGet制御方法（判明した範囲で）\n- ADR-0003プラグイン課題の要約\n- 調査できなかった項目とその理由"
    },
    {
      "id": "part-2-external-research",
      "title": "外部技術調査（OTel .NET実装詳細・MSBuildWorkspace NuGet制御）",
      "instruction": "## 目的\nローカルファイルでは補えない技術的詳細を外部から収集する。\n\n## 調査範囲\n\n### A. .NET csproj 条件付きPackageReference記法\n- `Condition` 属性を使った条件付き `PackageReference` の正しい記法（MSBuild公式ドキュメント）\n- `$(Configuration)` やカスタムプロパティ（例: `$(EnableOtel)`）を使った条件例\n- 参考URL候補: https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-conditions\n\n### B. MSBuildWorkspace NuGet自動復元制御\n- `Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace` でNuGet restoreを抑制する方法\n  - `Properties` ディクショナリ経由の `RestorePackages=false` 等\n  - `MSBuildWorkspace.OpenSolutionAsync` 時の設定\n- 未復元パッケージの場合のエラー処理パターン\n- 関連: GitHub roslyn-sdk, StackOverflow, 公式ドキュメント\n\n### C. OTel .NET SDK 起動コスト・サイズ\n- `OpenTelemetry` / `OpenTelemetry.Exporter.OpenTelemetryProtocol` の最新安定版バージョンとNuGetページのサイズ\n  - https://www.nuget.org/packages/OpenTelemetry\n  - https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol\n- 短寿命プロセス（CLI）での起動コスト・バッチflushに関するIssue議論（opentelemetry-dotnet GitHub Issue #5102, #2979）\n- `ForceFlush()` タイムアウト推奨値\n\n### D. OTel .NET STDERR汚染防止\n- OTel SDK自己テレメトリのSTDERR出力を無効化する方法\n- `OTEL_LOG_LEVEL=none` 環境変数の効果\n- コード内での明示的無効化パターン\n\n## 調査手段\n- Web検索: MSBuild公式ドキュメント、nuget.org、opentelemetry-dotnet GitHub、StackOverflow\n- WebFetch: nuget.orgパッケージページ等への直接アクセス\n\n## データ保存ルール\n- 調査結果を `/Users/r-hanaoka/Development/src/github.com/dora56/takt-worktrees/20260306T0214-cliniokeruobuzaababiriteitonet/.takt/runs/20260306-013809-cli/reports/data-otel-dotnet.md` に書き出す\n- 出典URL、取得日（2026-03-06）、生データを含める\n- 調査不可の項目は「調査不可」と理由を明記する\n\n## 報告構成\n- 条件付きPackageReference記法（コード例と出典URL）\n- MSBuildWorkspace NuGet制御方法（具体的なAPIとコード例、出典URL）\n- OTel SDK起動コスト・サイズ（数値と出典、調査不可の場合は明記）\n- ForceFlush推奨タイムアウト値（数値と根拠）\n- STDERR汚染防止のコードパターン（出典URL）\n- 調査できなかった項目とその理由\n- ADR-0004/0005実装への反映点まとめ"
    },
    {
      "id": "part-3-adr-creation",
      "title": "ADR文書の作成・更新と調査統合メモの出力",
      "instruction": "## 目的\n収集済みの調査データを元に、ADR文書4件を作成・更新し、調査統合メモを出力する。\n\n## 参照すべきファイル（すべて読んでから着手する）\n- `.takt/runs/20260306-013809-cli/reports/data-otel-patterns.md` — OTel実装パターン詳細（Pattern D コード含む）\n- `.takt/runs/20260306-013809-cli/reports/data-network-policy.md` — ネットワークポリシー比較\n- `.takt/runs/20260306-013809-cli/reports/data-internal-scan.md` — 内部コードスキャン結果\n- `.takt/runs/20260306-013809-cli/reports/research-report.md` — 調査統合レポート\n- `docs/adr/ADR-0001-output-contract.md` — 更新対象（Notesのみ追記）\n- `docs/adr/ADR-0002-sarif-strategy.md` — フォーマット参照用\n- `docs/adr/ADR-0003-plugin-safety-model.md` — フォーマット参照用\n- `docs/observability.md` — NF5.2/NF5.3仕様確認\n\n## 作業項目\n\n### 1. `docs/adr/ADR-0004-otel-observability-strategy.md` を新規作成\n\nフォーマット: 既存ADRと完全一致（`Status: Accepted` / `Date: 2026-03-06` はH1直下フラットテキスト、セクションはすべてH2）\n\n含めるべき内容:\n- **Context**: 「no telemetry by default」制約とオブザーバビリティ要件のトレードオフ。現行はNDJSON（NF5.2/NF5.3）でローカル出力を実現しているが、CI/CD統合のため構造化テレメトリのopt-in需要がある\n- **Decision**:\n  - `OTEL_EXPORTER_OTLP_ENDPOINT` 環境変数が未設定の場合、OTel SDKの初期化を完全スキップ（ゼロオーバーヘッド）\n  - 対象シグナル: Traces + Metrics のみ。Logsは既存NDJSON（NF5.2/NF5.3）が担当するため対象外\n  - OTel SDKパッケージは条件付きPackageReference（必須依存にしない）。Semgrep Issue #10408の教訓を根拠として記載\n  - Flush保証: `using var host = builder.Build()` パターンまたは `ForceFlush()` を明示的呼び出し（opentelemetry-dotnet Issue #5102参照）\n  - OTel自己テレメトリのSTDERR出力を禁止（`OTEL_LOG_LEVEL=none` または明示的無効化）\n  - 「no telemetry by default」「STDOUT汚染なし」「決定論的出力」すべての制約と両立することを明記\n- **Alternatives considered**: デフォルト有効化（却下: 制約違反）、CLIフラグ方式（却下: 標準OTel環境変数との重複）、OTel Logs採用（却下: デフォルト無効制約と矛盾）\n- **Notes**: ADR-0003プラグインワーカープロセスとのOTelコンテキスト伝播（W3C TraceContext相当）はPhase 2+以降の課題。IPCメッセージへのTraceId/SpanId埋め込みが必要になる見込み\n\n### 2. `docs/adr/ADR-0005-network-policy-air-gap.md` を新規作成\n\nフォーマット: 既存ADRと完全一致\n\n含めるべき内容:\n- **Context**: CLAUDE.md「no network calls」制約のスコープが曖昧。ビルド時NuGet restoreとランタイム通信を区別する必要がある。エアギャップCI環境での利用要件も存在する\n- **Decision**:\n  - 「no network calls」= ランタイム（分析実行中）のアウトバウンド通信禁止。ビルド時NuGet restoreは「事前工程」として除外\n  - 実行前提: `dotnet restore` 済みの環境での実行を前提とする\n  - MSBuildWorkspaceロード時の暗黙的NuGet restore防止: `EnableNuGetPackageRestore=false` 相当の設定を適用。パッケージ未キャッシュ時は「`dotnet restore` を先に実行してください」旨のエラーをSTDERRに出力\n  - バージョンチェック機能は実装しない（SARIFの `tool.driver.version` 埋め込みのみ）\n  - opt-in時のOTLP export（ADR-0004）は唯一のランタイム通信例外\n  - エアギャップCI対応: 事前NuGetキャッシュ + スタンドアローン実行ファイル配布を推奨ガイダンスとして記載\n- **Alternatives considered**: ランタイム中の条件付き通信許可（却下: 決定論的動作が損なわれる）、完全オフラインビルド強制（却下: 開発体験を過度に制限）\n- **Notes**: SonarScannerはサーバー必須設計であり、本ツールのローカル完結SARIF出力はエアギャップ環境での明確な差別化要素\n\n### 3. `docs/adr/ADR-0001-output-contract.md` の Notes セクションに追記のみ\n\n**既存テキストは一切変更・削除しない**。Notesセクションの末尾に以下2行を追加する:\n```\n- ネットワークポリシーの詳細スコープ定義（ランタイム vs ビルド時）は ADR-0005 を参照。\n- OTel opt-in オブザーバビリティ戦略は ADR-0004 を参照。\n```\n\n### 4. `.takt/runs/20260306-013809-cli/reports/dig-current-state.md` を作成（調査統合メモ）\n\n含めるべき内容:\n- 主要発見のまとめ（OTel Pattern D コード全文、NF5.2/NF5.3フィールド一覧、MSBuildWorkspace制御方法、プラグイン×OTel将来課題）\n- 作成したADR一覧と判断根拠の要約\n- 実装時の注意事項（STDERR汚染防止、Flush保証タイミング、条件付きPackageReferenceの記法）\n- 調査不可・未確認事項（あれば正直に記載）\n\n## データ品質ルール\n- 推測は「推測」と明記する\n- 根拠のない記述は入れない\n- 既存ADRフォーマットから逸脱しない（特にStatus/DateはH1直下フラットテキスト）\n\n## 完了条件\n- `docs/adr/ADR-0004-otel-observability-strategy.md` が存在する\n- `docs/adr/ADR-0005-network-policy-air-gap.md` が存在する\n- `docs/adr/ADR-0001-output-contract.md` のNotes以外が変更されていない\n- `.takt/runs/20260306-013809-cli/reports/dig-current-state.md` が存在する",
      "timeoutMs": 300000
    }
  ]
}

---

## part-1-internal-files: ローカルファイル調査（既存ADR・docs・前回レポートの精読）
調査完了。以下に報告します。

---

## 調査報告

### 1. ADRフォーマットのテンプレート

既存3件のADRから共通パターンを確認:

```markdown
# ADR-XXXX: タイトル
Status: Accepted
Date: YYYY-MM-DD

## Context
## Decision
## Consequences
## Alternatives considered
## Notes
```

- H1にタイトル、`Status:`/`Date:`はH1直下にフラットテキスト（見出しなし）
- 全セクションH2（`##`）統一
- Decision内は`-`箇条書き、サブ項目はインデントで表現

---

### 2. 「no network calls / no telemetry」正確な文言

出典: `docs/adr/ADR-0001-output-contract.md` Decisionセクション L23

```
- Local-only execution: no network calls, no telemetry by default.
```

CLAUDE.md（AGENTS.md）では `**no network calls**` と太字強調されているが意味は同一。

---

### 3. ADR-0003 プラグイン × OTelの課題

ADR-0003はPhase 2+でプラグインを**独立したワーカープロセス**で実行すると決定。OTelをopt-in導入した場合に発生する問題:

1. **コンテキスト伝播**: TraceId/SpanIdをIPCメッセージ（名前付きパイプ等）に含める設計が必要（W3C TraceContext相当）
2. **ワーカー側SDK**: ワーカープロセスでスパンを継続するには同側にもOTel SDKが必要
3. **AGSAFE9001/9002との整合**: timeout/crashエラー報告とスパン終了タイミングが複雑になる

重要度: 低（Phase 2以降、MVP非該当）

---

### 4. 現行NDJSON仕様（NF5.2/NF5.3）

出典: `docs/observability.md`

**NF5.2**（必須シグナル → STDERRまたはログファイル）:
- Timing: total + workspace_load / rules / output_write フェーズ別
- Scale: projects count, documents count
- Quality: rules executed count, findings by level
- Failure: workspace load failure / rule execution failure / unexpected exception

**NF5.3**（`--log-format json`時）:
- NDJSON形式（1行1JSONオブジェクト）
- フィールド: `event`, `runId`, `toolVersion`, `command`, `path`, `format`, `exitCode`, `durMs`, `phase`, `counts`, `error`

**OTel Logsで置き換えない理由**: NDJSONはデフォルトでSTDERR/ファイルへのローカル出力が前提。OTel Logsはデフォルトオフ（「no telemetry by default」制約）と矛盾する。置き換えでなくopt-in追加シグナルとして共存すべき。

---

### 5. OTel実装コードスニペット（パターンD）

出典: `data-otel-patterns.md` §4 パターンD

```csharp
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
if (otlpEndpoint is not null)
{
    services.AddOpenTelemetry()
        .WithTracing(b => b
            .AddSource("Roslyntic")
            .AddOtlpExporter())  // エンドポイントは env var から自動読み込み
        .WithMetrics(b => b
            .AddMeter("Roslyntic")
            .AddOtlpExporter());
}
// using var host = builder.Build(); で自動Flush（IHost.Dispose()経由）
```

---

### 6. MSBuildWorkspace NuGet制御

出典: `data-network-policy.md` §2-E

| 手段 | 正確なAPI/設定 |
|---|---|
| 事前restore | `dotnet restore <solution>` |
| 環境変数 | `EnableNuGetPackageRestore=false` |
| NuGet.Config | `<add key="enabled" value="False" />` |
| プロパティ指定 | `RestorePackagesPath` |
| CLIフラグ | `dotnet build --no-restore` |

---

### 7. OTel SDKパッケージ

出典: `data-otel-patterns.md` §3（NuGet.org 2026-03-06取得）

| パッケージ | バージョン | サイズ |
|---|---|---|
| `OpenTelemetry` | 1.15.0 | 782.97 KB |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.15.0 | 489.93 KB |
| `OpenTelemetry.Extensions.Hosting` | 1.15.0 | 89.4 KB |
| **合計** | — | **≒1.36 MB** |

起動コストの実測値は未発見（公開ベンチマークなし）。数十ms程度と推定（推測値）。

---

### 8. csproj現在状態

出典: `Roslyntic.Cli/Roslyntic.Cli.csproj`（直接読み込み）

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

**PackageReference: 0件**。`Roslyntic.Core`等のプロジェクトは`.csproj`未作成。

---

### 調査できなかった項目

| 項目 | 理由 |
|---|---|
| 他プロジェクト（Core/Analysis/Rules/Sarif/Tests）のPackageReference | `.csproj`が未作成（スケルトン段階） |
| .NET OTel SDK初期化コストの実測値 | 公開ベンチマークが存在しない |
| MSBuildWorkspaceのNuGet復元実動作 | ローカル実行が必要（Web調査範囲外） |

調査結果は `.takt/runs/20260306-013809-cli/reports/data-internal-state.md` に書き出し済みです。

---

## part-2-external-research: 外部技術調査（OTel .NET実装詳細・MSBuildWorkspace NuGet制御）
レポートを `/Users/r-hanaoka/Development/src/github.com/dora56/takt-worktrees/20260306T0214-cliniokeruobuzaababiriteitonet/.takt/runs/20260306-013809-cli/reports/data-otel-dotnet.md` に書き出しました。

---

## 調査結果サマリー

### A. 条件付き PackageReference 記法

MSBuild公式ドキュメントより確認。`$(EnableOtel)` のようなカスタムプロパティを `ItemGroup` の `Condition` 属性で使う方法が最もクリーン：

```xml
<ItemGroup Condition="'$(EnableOtel)' == 'true'">
  <PackageReference Include="OpenTelemetry" Version="1.15.0" />
</ItemGroup>
```

Boolean ガード節（`Condition="'$(EnableOtel)' == ''"` でデフォルト値設定）も合わせて実装推奨。

---

### B. MSBuildWorkspace NuGet 制御

`MSBuildWorkspace.Create(properties)` の Properties ディクショナリに以下を渡すことで design-time build をNuGet restoreなしで実行：

```csharp
var properties = new Dictionary<string, string>
{
    { "DesignTimeBuild", "true" },
    { "BuildingInsideVisualStudio", "true" },
};
using var workspace = MSBuildWorkspace.Create(properties);
workspace.WorkspaceFailed += (_, args) => Console.Error.WriteLine(args.Diagnostic.Message);
```

未復元パッケージはエラーではなく **診断（WorkspaceFailed）** として現れるため、`WorkspaceFailed` イベントハンドラが必須。

---

### C. OTel SDK サイズ（2026-01-21 リリース）

| パッケージ | バージョン | サイズ |
|-----------|-----------|-------|
| `OpenTelemetry` | 1.15.0 | 782.97 KB |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.15.0 | 489.93 KB |

短命CLIプロセスでのflush問題（Issue #5102）は **`using` によるDispose** で解決可能。

---

### D. ForceFlush 推奨タイムアウト

- API デフォルト: `Timeout.Infinite`（明示的指定なし）
- **CLI向け推奨**: `ForceFlush(timeoutMilliseconds: 10_000)` + BatchExporter の `ExporterTimeoutMilliseconds = 1_000`
- `using` ブロックによる Dispose が最も確実

---

### E. STDERR 汚染防止

OTel SDKは **EventSource** ベースで内部ログを出力するが、デフォルトでSTDERRには出ない。完全抑制は `EventListener` を継承した `SilentOtelEventListener` で実装可能。`OTEL_LOG_LEVEL=none` は自動計装専用で手動SDK組み込みには効果なし（**要注意**）。

---

### 調査不可項目

- `SkipMetadataImportOnMissingAssemblies` の詳細（GitHub 404でソースアクセス不能）
- OTel SDK 起動時間のミリ秒単位ベンチマーク（公式数値なし、実測必要）

---

## part-3-adr-creation: ADR文書の作成・更新と調査統合メモの出力
すべての作業が完了しました。以下に実施内容を報告します。

---

## 作業完了報告

### 実施した作業

#### 1. `docs/adr/ADR-0004-otel-observability-strategy.md` — 新規作成 ✅

既存ADRフォーマット（Status/DateはH1直下フラットテキスト、セクションはH2）に完全準拠して作成。

**主な決定内容**:
- `OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時は OTel SDK 初期化を完全スキップ（ゼロオーバーヘッド）
- 対象シグナル: Traces + Metrics のみ（Logs は NF5.2/NF5.3 NDJSON が担当）
- OTel SDK は条件付き PackageReference（Semgrep Issue #10408 を根拠として明記）
- Flush 保証: `using var host = builder.Build()` パターンまたは `ForceFlush()` 明示呼び出し（opentelemetry-dotnet Issue #5102 参照）
- OTel 自己テレメトリの STDERR 出力禁止（`OTEL_LOG_LEVEL=none`）
- Pattern D のコードサンプルを Decision セクションに埋め込み

#### 2. `docs/adr/ADR-0005-network-policy-air-gap.md` — 新規作成 ✅

**主な決定内容**:
- 「no network calls」= ランタイム時アウトバウンド通信禁止、ビルド時 NuGet restore は対象外
- `dotnet restore` 済みの環境での実行を前提条件とし、ドキュメントへの明記を決定
- `EnableNuGetPackageRestore=false` 相当の設定適用。未 restore 時は STDERR エラー + 終了コード 2
- バージョンチェック機能は実装しない（SARIF `tool.driver.version` 埋め込みのみ）
- opt-in 時の OTLP export を唯一のランタイム通信例外として明記
- エアギャップ CI 対応ガイダンス（事前 NuGet キャッシュ + スタンドアローン実行ファイル配布）を記載

#### 3. `docs/adr/ADR-0001-output-contract.md` — Notes セクション末尾に2行追記 ✅

既存テキストは一切変更せず、Notes 末尾に以下を追記:
```
- ネットワークポリシーの詳細スコープ定義（ランタイム vs ビルド時）は ADR-0005 を参照。
- OTel opt-in オブザーバビリティ戦略は ADR-0004 を参照。
```

#### 4. `.takt/runs/20260306-013809-cli/reports/dig-current-state.md` — 新規作成 ✅

調査統合メモとして以下を網羅:
- OTel Pattern D コード全文（出典 URL 付き）
- NF5.2/NF5.3 フィールド一覧
- MSBuildWorkspace ネットワーク制御方法（4手段）
- プラグイン × OTel 将来課題
- 作成 ADR 一覧と判断根拠の要約
- 実装時の注意事項（STDERR 汚染防止、Flush 保証タイミング、条件付き PackageReference の記法）
- 調査不可・未確認事項（推測は「推測」と明記、根拠なし記述なし）