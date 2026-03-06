# 外部調査レポート: OTel .NET & MSBuild 技術詳細

- 調査日: 2026-03-06
- 担当: dig.part-2-external-research
- 目的: ADR-0004（OTel計装）・ADR-0005（MSBuildWorkspace）実装に必要な技術詳細の収集

---

## A. .NET csproj 条件付き PackageReference 記法

### 基本構文

`Condition` 属性は MSBuild の任意の要素に付与可能。文字列値はシングルクォートで囲む。

```xml
<!-- 個別PackageReferenceへの条件付与 -->
<ItemGroup>
  <PackageReference Include="Newtonsoft.Json" Version="9.0.1"
      Condition="'$(TargetFramework)' == 'net452'" />
</ItemGroup>

<!-- ItemGroup全体への条件付与（配下のPackageReferenceすべてに適用） -->
<ItemGroup Condition="'$(TargetFramework)' == 'net452'">
  <PackageReference Include="Newtonsoft.Json" Version="9.0.1" />
  <PackageReference Include="Contoso.Utility.UsefulStuff" Version="3.6.0" />
</ItemGroup>
```

### $(Configuration) を使った例

```xml
<!-- Debugビルドのみ計装パッケージを有効化 -->
<ItemGroup>
  <PackageReference Include="OpenTelemetry" Version="1.15.0"
      Condition="'$(Configuration)' == 'Debug'" />
</ItemGroup>
```

### カスタムプロパティ（$(EnableOtel)等）を使った例

```xml
<!-- Directory.Build.props または csproj 内でプロパティ定義 -->
<PropertyGroup>
  <EnableOtel Condition="'$(EnableOtel)' == ''">false</EnableOtel>
</PropertyGroup>

<!-- カスタムプロパティによる条件付きPackageReference -->
<ItemGroup Condition="'$(EnableOtel)' == 'true'">
  <PackageReference Include="OpenTelemetry" Version="1.15.0" />
  <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.0" />
</ItemGroup>
```

### MSBuild Boolean 評価の注意点

- `'$(Prop)' == 'true'` → Propが`true`の場合
- `'$(Prop)' != 'false'` → Propが`true`**または未設定または別の値**の場合（意図しない挙動に注意）
- ガード節として `Condition="'$(EnableOtel)' == ''" ` でデフォルト値を設定するパターンが推奨

### 出典

- [MSBuild Conditions - Microsoft Learn](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-conditions?view=vs-2022)
  （更新日: 2024-10-04）
- [NuGet PackageReference in project files - Microsoft Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files)
  （更新日: 2026-03-03）

---

## B. MSBuildWorkspace NuGet 自動復元制御

### MSBuildWorkspace の動作原理

`MSBuildWorkspace.OpenSolutionAsync()` / `OpenProjectAsync()` は **design-time build** を実行する。
Design-time build は：
- バイナリ（.dll）を生成しない
- ソースファイル・参照・コンパイルオプションの取得に特化
- 通常の NuGet restore とは異なる特殊なビルドパス

### Properties ディクショナリによる制御

```csharp
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;

// MSBuildLocatorの登録は必須（OpenSolutionAsync前に呼ぶ）
MSBuildLocator.RegisterDefaults();

var properties = new Dictionary<string, string>
{
    // Design-time build フラグ（NuGet restoreをスキップするヒント）
    { "DesignTimeBuild", "true" },
    // Visual Studio内部ビルドとして扱う
    { "BuildingInsideVisualStudio", "true" },
    // システムランタイム依存のチェックを有効化（参照解決改善）
    { "CheckForSystemRuntimeDependency", "true" },
};

using var workspace = MSBuildWorkspace.Create(properties);

// 診断情報の収集（ロード失敗時の原因調査用）
workspace.WorkspaceFailed += (sender, args) =>
{
    Console.Error.WriteLine($"[MSBuildWorkspace] {args.Diagnostic.Kind}: {args.Diagnostic.Message}");
};

var solution = await workspace.OpenSolutionAsync("path/to/Solution.sln");
```

### NuGet 未復元時のエラー処理パターン

```csharp
var solution = await workspace.OpenSolutionAsync(solutionPath);

// ロード後に診断を確認
var failures = workspace.Diagnostics
    .Where(d => d.Kind == WorkspaceDiagnosticKind.Failure)
    .ToList();

if (failures.Any())
{
    // 未復元パッケージ起因のエラーはメタデータ参照不足として現れる
    foreach (var f in failures)
        Console.Error.WriteLine($"Workspace load failure: {f.Message}");
}
```

### 重要な注意事項

- `MSBuildLocator.RegisterDefaults()` を **`MSBuildWorkspace.Create()` より先に呼ぶ**こと（これを忘れると MSBuild アセンブリが解決できず例外）
- Design-time build は NuGet restore を通常トリガーしないが、**packages が未復元の場合メタデータ参照が失敗する**（診断に警告として現れる）
- `SkipMetadataImportOnMissingAssemblies` プロパティ: Roslynソースコードには存在するが公開 API ドキュメント未記載のため実用は要検証

### 出典

- [Using MSBuildWorkspace - DustinCampbell Gist](https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3)
- [Roslyn msbuildworkspace Compiling WPF project · Issue #29780 · dotnet/roslyn](https://github.com/dotnet/roslyn/issues/29780)
- [NuGet Gallery | Microsoft.CodeAnalysis.Workspaces.MSBuild 5.0.0](https://www.nuget.org/packages/Microsoft.CodeAnalysis.Workspaces.MSBuild/)

---

## C. OTel .NET SDK 起動コスト・サイズ

### 最新安定版バージョン（2026-03-06 時点）

| パッケージ | バージョン | リリース日 | パッケージサイズ | シンボルサイズ |
|-----------|-----------|-----------|----------------|--------------|
| `OpenTelemetry` | **1.15.0** | 2026-01-21 | 782.97 KB | 277.21 KB |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | **1.15.0** | 2026-01-21 | 489.93 KB | 200.6 KB |

### 依存関係（OpenTelemetry 1.15.0）

- `Microsoft.Extensions.Diagnostics.Abstractions` (≥ 8.0.0 for .NET 8)
- `Microsoft.Extensions.Logging.Configuration` (≥ 8.0.0 for .NET 8)
- `OpenTelemetry.Api.ProviderBuilderExtensions` (≥ 1.15.0)

### 依存関係（OpenTelemetry.Exporter.OpenTelemetryProtocol 1.15.0）

- `OpenTelemetry` (≥ 1.15.0)
- `Microsoft.Extensions.Configuration.Binder` (≥ 8.0.2, .NET 8/10のみ)

### 短命プロセス（CLI）でのflush問題

#### Issue #5102: OTLP ログが短命プロセスでexportされない
- **URL**: https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102
- **症状**: 1秒未満のCLIアプリでOTLPにログが届かない（コンソールには表示される）
- **原因**: BatchExportProcessor がプロセス終了前にフラッシュされない
- **解決策**:
  - `LoggerFactory` を `using` でラップして確実に `Dispose` する（Dispose がFlushをトリガー）
  - `loggerProvider.ForceFlush()` を明示的に呼ぶ
  - `ExportProcessorType.Simple` はログには効かない（トレースのみ）

#### Issue #2979: メトリクス自動flushの停止
- **URL**: https://github.com/open-telemetry/opentelemetry-dotnet/issues/2979
- **症状**: v1.2.0-rc3 でメトリクスの自動flush機能が失われた
- **原因**: 意図しない破壊的変更
- **解決策**: `MetricReaderType.Periodic` を明示的に設定

### 出典

- [NuGet: OpenTelemetry 1.15.0](https://www.nuget.org/packages/OpenTelemetry)
- [NuGet: OpenTelemetry.Exporter.OpenTelemetryProtocol 1.15.0](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)
- [Issue #5102](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102)
- [Issue #2979](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2979)

---

## D. ForceFlush 推奨タイムアウト値

### API シグネチャ

```csharp
// BaseProcessor.cs
public bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
```

デフォルトは `Timeout.Infinite`（無限待機）。

### 推奨タイムアウト値（根拠付き）

| シナリオ | 推奨値 | 根拠 |
|---------|-------|------|
| CLI プロセス終了時 ForceFlush | **10,000 ms** | OTel仕様・Pythonリファレンス実装の推奨値 |
| Exporter タイムアウト (BatchExporter) | **1,000 ms** | CLIでの実用的な上限値（Issue #5261より） |
| Shutdown 全体のタイムアウト | **30,000 ms** | OTel仕様 SDK Shutdown SHOULD complete or abort |

### CLI アプリでの推奨実装パターン

```csharp
// パターン1: using によるDispose（最も確実）
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddOtlpExporter()
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddOtlpExporter()
    .Build();

// ... CLIの処理 ...

// パターン2: 明示的 ForceFlush（タイムアウト付き）
tracerProvider.ForceFlush(timeoutMilliseconds: 10_000);
meterProvider.ForceFlush(timeoutMilliseconds: 10_000);

// パターン3: DI 使用時
app.Lifetime.ApplicationStopped.Register(() =>
{
    app.Services.GetRequiredService<TracerProvider>()
        .ForceFlush(timeoutMilliseconds: 10_000);
    app.Services.GetRequiredService<MeterProvider>()
        .ForceFlush(timeoutMilliseconds: 10_000);
});
```

### BatchExporter チューニング（CLI向け）

```csharp
.AddOtlpExporter((exporterOptions, processorOptions) =>
{
    exporterOptions.Endpoint = new Uri("http://localhost:4317");
    processorOptions.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
    {
        MaxQueueSize = 8192,
        MaxExportBatchSize = 1024,
        ScheduledDelayMilliseconds = 200,        // 短命プロセス向けに短縮
        ExporterTimeoutMilliseconds = 1_000,     // ネットワーク障害時の上限
    };
})
```

### 出典

- [BaseProcessor.cs - open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/BaseProcessor.cs)
- [Issue #5261: graceful shutdown](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5261)
- [Issue #4858: ForceFlush guarantee](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4858)
- [OTel Tracing SDK Specification](https://opentelemetry.io/docs/specs/otel/trace/sdk/)

---

## E. STDERR 汚染防止のコードパターン

### OTel .NET SDK の内部ログの仕組み

- SDK 内部ログは **EventSource**（名前: `"OpenTelemetry-Sdk"`）経由で出力
- EventSource は STDERR への直接書き込みではなく、リスナーへのイベント配信
- デフォルトでは **OTEL_DIAGNOSTICS.json** ファイルが存在しない限り、自己診断ログはファイルにも STDERR にも出力されない

### 方法1: OTEL_DIAGNOSTICS.json ファイル不使用（デフォルト）

ファイルが存在しなければ自己診断機能は無効。CLIツールの場合は**デフォルト動作で問題ない**。

### 方法2: EventListener で完全抑制（コード内での明示的制御）

```csharp
using System.Diagnostics.Tracing;

/// <summary>
/// OpenTelemetry SDK 内部ログを STDERR に出さないための EventListener。
/// 必要なら独自ロガーに転送できる。
/// </summary>
internal sealed class SilentOtelEventListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // "OpenTelemetry-" で始まるすべての EventSource を対象外に
        if (eventSource.Name.StartsWith("OpenTelemetry-", StringComparison.Ordinal))
        {
            // イベントを有効化しない（= 完全無視）
            return;
        }
        base.OnEventSourceCreated(eventSource);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // 何もしない = STDERR への漏洩なし
    }
}

// 使用例（CLIアプリのエントリーポイント）
using var _ = new SilentOtelEventListener(); // アプリ存続期間中保持
```

### 方法3: EventListener でSTDERR→STDERRではなくログファイルへ転送

```csharp
internal sealed class OtelDiagnosticsToFileListener : EventListener
{
    private readonly StreamWriter _writer;

    public OtelDiagnosticsToFileListener(string logPath)
    {
        _writer = new StreamWriter(logPath, append: true);
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name.StartsWith("OpenTelemetry-", StringComparison.Ordinal))
        {
            // Warning 以上のみキャプチャ
            EnableEvents(eventSource, EventLevel.Warning);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // STDERR ではなくファイルへ書く
        _writer.WriteLine($"[OTel:{eventData.Level}] {eventData.EventSource.Name}: {eventData.Message}");
    }

    public override void Dispose()
    {
        _writer.Dispose();
        base.Dispose();
    }
}
```

### OTEL_LOG_LEVEL 環境変数について

- `OTEL_LOG_LEVEL=none` は **OpenTelemetry .NET 自動計装（Auto Instrumentation）** 向けの環境変数
- 手動で SDK を組み込む場合（`Sdk.CreateTracerProviderBuilder()` 等）への効果は**限定的または無効**
- 手動組み込みの場合は上記の EventListener パターンを使用すること

### 出典

- [opentelemetry-dotnet/src/OpenTelemetry/README.md](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/README.md)
- [SdkSelfDiagnosticsEventListener.cs - opentelemetry-dotnet-instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation/blob/ac0ddf9a71d3883b736a60ac14bd258cff6ebf41/src/OpenTelemetry.AutoInstrumentation/Diagnostics/SdkSelfDiagnosticsEventListener.cs)
- [Troubleshooting | OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/troubleshooting/)

---

## 調査できなかった項目

| 項目 | 理由 |
|------|------|
| MSBuildWorkspace の `SkipMetadataImportOnMissingAssemblies` プロパティの詳細 | GitHub dotnet/roslyn の該当ファイル (MSBuildWorkspace.cs) が 404 でアクセス不能。Roslyn ソースブラウザ経由での確認が必要。公式ドキュメントにも記載なし |
| OTel SDK の起動時間（ミリ秒単位のオーバーヘッド） | ベンチマーク数値を記載した公式資料・Issue が見つからず。実測が必要 |
| ForceFlush タイムアウト「公式推奨値」 | OTel 仕様は "SHOULD complete or abort within some timeout" とだけ述べており数値未規定。10秒はコミュニティでの実用値 |

---

## ADR-0004 / ADR-0005 実装への反映点まとめ

### ADR-0004（OTel計装）

1. **条件付きPackageReference**: `$(EnableOtel)`カスタムプロパティ + ItemGroup Condition で本番/開発を切り替え可能
   ```xml
   <ItemGroup Condition="'$(EnableOtel)' == 'true'">
     <PackageReference Include="OpenTelemetry" Version="1.15.0" />
     <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.0" />
   </ItemGroup>
   ```

2. **パッケージサイズ**: OpenTelemetry(783KB) + OTLP Exporter(490KB) = 合計約 1.3MB のパッケージ追加（起動コストへの影響は実測で確認要）

3. **CLIでの確実なflush**: `using var tracerProvider = ...` + `using var meterProvider = ...` パターンを採用。追加で `ForceFlush(10_000)` を呼ぶ

4. **STDERR汚染防止**: `SilentOtelEventListener` をエントリポイントで生成・保持。SDKのデフォルト動作は既にSTDERR非汚染だが、明示的な制御として実装

5. **バッチエクスポーターのタイムアウト**: `ExporterTimeoutMilliseconds = 1000` に設定（CLI向け）

### ADR-0005（MSBuildWorkspace）

1. **MSBuildLocatorの登録**: `MSBuildLocator.RegisterDefaults()` → `MSBuildWorkspace.Create(properties)` の順を厳守

2. **Propertiesディクショナリ**: `DesignTimeBuild=true` + `BuildingInsideVisualStudio=true` を設定してNuGet restoreをスキップ

3. **WorkspaceFailedイベント**: ロード失敗（未復元パッケージ含む）を STDERR へ出力するリスナーを必ず登録

4. **未復元パッケージ**: MSBuildWorkspace はエラーにはならず診断に記録される。Roslynticは「解析可能なプロジェクトのみ処理、残りは警告」というフォールバック戦略が適切
