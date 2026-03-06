# ADR-0004: OpenTelemetry Observability Strategy (opt-in)
Status: Accepted
Date: 2026-03-06

## Context
CLAUDE.md は「no telemetry by default」「no network calls」を非交渉的制約として規定している。
一方、CI/CD 統合・パフォーマンス分析の需要から、構造化テレメトリ（Traces / Metrics）の opt-in 出力要件が存在する。

現行の observability 実装は NDJSON（NF5.2 / NF5.3）によるローカル出力（STDERR またはログファイル）で実現しており、ランタイム中のネットワーク通信はゼロである。
しかし、外部オブザーバビリティ基盤（Jaeger, Prometheus, Grafana 等）との統合を必要とする CI/CD 環境では、OTLP 経由の構造化テレメトリ出力が有効な手段となる。

この需要に応えるにあたり、「no telemetry by default」「STDOUT 汚染なし」「決定論的出力」の三制約すべてと両立する実装方針を決定する必要がある。

## Decision
- **デフォルト無効**: 環境変数 `OTEL_EXPORTER_OTLP_ENDPOINT` が未設定の場合、OTel SDK の初期化を完全にスキップする（ゼロオーバーヘッド）。SDK の初期化コードすら実行しない。
- **opt-in 手段**: `OTEL_EXPORTER_OTLP_ENDPOINT` を設定するだけで有効化される（OTel 標準環境変数を唯一のゲートとする）。
- **対象シグナル**: Traces と Metrics のみ。Logs は既存 NDJSON（NF5.2 / NF5.3）が担当するため OTel Logs シグナルは対象外とする。
- **条件付き PackageReference**: OTel SDK パッケージ（`OpenTelemetry`、`OpenTelemetry.Exporter.OpenTelemetryProtocol`、`OpenTelemetry.Extensions.Hosting`）は条件付き参照とし、必須依存関係にしない。Semgrep が opt-in 機能のために OTel SDK を必須依存関係として含めた結果、全ユーザーにパッケージサイズ増大・依存関係競合の影響が生じた教訓（Issue [#10408](https://github.com/semgrep/semgrep/issues/10408)）を根拠とする。
- **Flush 保証**: `using var host = builder.Build()` パターンによる `IHost.Dispose()` 経由の自動 Flush、または `TracerProvider.ForceFlush()` / `MeterProvider.ForceFlush()` の明示的呼び出しを必須とする。短寿命プロセスでは `BatchSpanProcessor`（デフォルト 5000ms 間隔）が export 完了前にプロセスが終了するリスクがあるため（[opentelemetry-dotnet Issue #5102](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102) 参照）、Flush を明示的に保証する。
- **OTel 自己テレメトリの STDERR 出力を禁止**: OTel SDK 自身のデバッグログが STDERR に出力されると「STDERR は人間向け診断情報」という ADR-0001 の契約が乱れる。`OTEL_LOG_LEVEL=none` の設定または SDK の自己テレメトリの明示的無効化を実装時に必須とする。
- **制約との整合性**: 上記すべての方針により、「no telemetry by default」「STDOUT 汚染なし」「決定論的出力」のすべての非交渉的制約と両立する。

実装例（.NET、Pattern D）:
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
// using var host = builder.Build(); で自動 Flush 保証（IHost.Dispose() 経由）
```

## Consequences
- Easier: 環境変数一つで外部オブザーバビリティ基盤との統合が可能になる。
- Easier: デフォルト無効のため、既存の「no telemetry by default」「ローカル実行」の保証は維持される。
- Easier: SARIF/JSON 出力（STDOUT）への影響がゼロ。決定論的出力に影響しない。
- Harder: 条件付き PackageReference の記法管理が必要（`<PackageReference Condition="...">` または別プロジェクト分離）。
- Harder: Flush 保証のためにプロセスライフサイクル管理に OTel の終了処理を組み込む必要がある。
- Risk: OTel SDK が STDERR にデバッグ出力を行う可能性があり、明示的な無効化を忘れると ADR-0001 に違反する。

## Alternatives considered
- **デフォルト有効化**（却下: 「no telemetry by default」制約に直接違反する）。
- **CLI フラグ方式**（例: `--otel-endpoint`）（却下: OTel 標準環境変数 `OTEL_EXPORTER_OTLP_ENDPOINT` との二重管理になり、標準 OTel エコシステムとの互換性が低下する）。
- **OTel Logs 採用**（却下: NF5.2 / NF5.3 の NDJSON が既にローカルログを担保しており、Logs シグナルを有効化するとデフォルト無効制約と矛盾する構成になりやすい）。

## Notes
- ADR-0003 のプラグインワーカープロセスとの OTel コンテキスト伝播（W3C TraceContext 相当）は Phase 2+ 以降の課題。プラグインワーカーへの TraceId / SpanId 埋め込みが IPC メッセージ設計に影響する見込みであり、Phase 2 の別途 ADR で扱う。
- ネットワークポリシーの詳細スコープ定義（OTel OTLP export がランタイム通信例外として許容される根拠）は ADR-0005 を参照。
