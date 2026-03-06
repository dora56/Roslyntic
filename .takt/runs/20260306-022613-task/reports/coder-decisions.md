# 決定ログ

## 1. Diagnostic 型の名前衝突を global using alias で解決
- **背景**: `Roslyntic.Rules` プロジェクトは `Microsoft.CodeAnalysis` と `Roslyntic.Core` の両方を参照するため、`Diagnostic` 型が曖昧参照エラー（CS0104）になった
- **検討した選択肢**: (a) 完全修飾名を使用、(b) `extern alias`、(c) `GlobalUsings.cs` に global using alias を定義
- **理由**: `GlobalUsings.cs` に `global using RoslynticDiagnostic = Roslyntic.Core.Diagnostic;` を定義するのが最もシンプルで、ファイルごとの using 追加が不要。`IAsyncEnumerable<RoslynticDiagnostic>` として IRule インターフェースと型が一致することも確認済み

## 2. Roslyntic.Core の CodeAnalysis バージョンを 4.13.0 に統一
- **背景**: Roslyntic.Core が `Microsoft.CodeAnalysis.CSharp.Workspaces 4.12.0`、CLI が `Microsoft.CodeAnalysis.Workspaces.MSBuild 4.13.0` を参照し、NU1608 警告が発生した
- **検討した選択肢**: (a) Core を 4.13.0 に上げる、(b) CLI を 4.12.0 に下げる
- **理由**: MSBuild Workspaces 4.13.0 がローカルキャッシュに存在し利用可能。バージョンを揃えることで警告を除去できる。テストプロジェクトの `Microsoft.CodeAnalysis.CSharp 4.12.0` は互換性があり動作に問題なし

## 3. サンプルプロジェクトを従来の .sln 形式で作成
- **背景**: 統合テスト（`CheckCommandIntegrationTests`）が `samples/Samples.sln` を参照しており、MSBuildWorkspace でロードする必要がある
- **検討した選択肢**: (a) `.slnx` 形式、(b) 従来の `.sln` 形式
- **理由**: `MSBuildWorkspace.OpenSolutionAsync` が標準の `.sln` 形式を確実にサポートするため `.sln` を選択。`.slnx` は新形式であり MSBuildWorkspace のサポート状況が不確実

## 4. LayerViolationRule のレイヤー検出を ReferencedAssemblyNames で実施
- **背景**: プロジェクト参照からレイヤー依存を検出する実装方法の選択が必要だった
- **検討した選択肢**: (a) `Project.AllProjectReferences` を直接列挙してワークスペースで解決、(b) `Compilation.ReferencedAssemblyNames` を使用
- **理由**: `Compilation.ReferencedAssemblyNames` はアセンブリ名（= プロジェクトの AssemblyName）を返すため `LayerClassifier.Classify` と直接連携できる。`AllProjectReferences` はプロジェクト ID を返すため追加のワークスペース解決が必要になり複雑度が増す