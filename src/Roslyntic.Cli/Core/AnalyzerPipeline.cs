using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;

internal static class AnalyzerPipeline
{
    private static readonly HashSet<(Layer From, Layer To)> ForbiddenEdges =
    [
        (Layer.Ui, Layer.Infrastructure),
        (Layer.Ui, Layer.Domain),
        (Layer.Domain, Layer.Infrastructure)
    ];

    public static async Task<AnalysisExecutionResult> AnalyzeAsync(string targetPath, CancellationToken cancellationToken)
    {
        IReadOnlyList<LoadedProject> projects;
        var workspaceLoadStopwatch = Stopwatch.StartNew();
        try
        {
            if (!File.Exists(targetPath))
            {
                throw new FileNotFoundException($"Target path was not found: {targetPath}");
            }

            projects = await WorkspaceProjectLoader.LoadAsync(targetPath, cancellationToken);
            workspaceLoadStopwatch.Stop();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new WorkspaceLoadException("Workspace loading failed.", ex);
        }

        var rulesStopwatch = Stopwatch.StartNew();
        var findings = new List<DiagnosticFinding>();
        try
        {
            findings.AddRange(await AnalyzeLayerViolationsAsync(projects, cancellationToken));

            foreach (var project in projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                findings.AddRange(await AnalyzeComplexityAsync(project.ProjectDirectoryPath, cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RuleExecutionException("Rule execution failed.", ex);
        }

        rulesStopwatch.Stop();
        var documents = projects.Sum(project => project.Compilation.SyntaxTrees.Count());
        var metrics = new AnalysisMetrics(
            workspaceLoadStopwatch.ElapsedMilliseconds,
            rulesStopwatch.ElapsedMilliseconds,
            projects.Count,
            documents,
            CliContract.RulesExecutedCount);
        return new AnalysisExecutionResult(findings, metrics);
    }

    private static async Task<IReadOnlyList<DiagnosticFinding>> AnalyzeLayerViolationsAsync(
        IReadOnlyList<LoadedProject> projects,
        CancellationToken cancellationToken)
    {
        var findings = new List<DiagnosticFinding>();
        var projectsByAssemblyName = projects
            .Where(project => !string.IsNullOrWhiteSpace(project.AssemblyName))
            .ToDictionary(project => project.AssemblyName, project => project, StringComparer.Ordinal);

        foreach (var project in projects)
        {
            if (!project.Layer.HasValue)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            foreach (var syntaxTree in project.Compilation.SyntaxTrees.OrderBy(tree => tree.FilePath, StringComparer.Ordinal))
            {
                var semanticModel = project.Compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync(cancellationToken);

                foreach (var node in root.DescendantNodes())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!TryResolveDependency(node, semanticModel, out var dependencySymbol, out var dependencyKind))
                    {
                        continue;
                    }

                    var targetProject = ResolveTargetProject(projectsByAssemblyName, dependencySymbol.ContainingAssembly, project.AssemblyName);
                    if (targetProject is null || !targetProject.Layer.HasValue)
                    {
                        continue;
                    }

                    if (!ForbiddenEdges.Contains((project.Layer.Value, targetProject.Layer.Value)))
                    {
                        continue;
                    }

                    var lineSpan = node.GetLocation().GetLineSpan();
                    var startPosition = lineSpan.StartLinePosition;
                    var endPosition = lineSpan.EndLinePosition;
                    var properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["fromLayer"] = project.Layer.Value.ToString(),
                        ["toLayer"] = targetProject.Layer.Value.ToString(),
                        ["fromSymbol"] = project.Name,
                        ["toSymbol"] = dependencySymbol.ToDisplayString(),
                        ["dependencyKind"] = dependencyKind
                    };

                    findings.Add(new DiagnosticFinding(
                        CliContract.ArchRuleId,
                        "warning",
                        $"Forbidden layer dependency: {project.Layer} -> {targetProject.Layer}",
                        new SourceLocation(
                            syntaxTree.FilePath ?? project.ProjectFilePath,
                            startPosition.Line + 1,
                            startPosition.Character + 1,
                            endPosition.Line + 1,
                            endPosition.Character + 1),
                        properties));
                }
            }
        }

        return findings;
    }

    private static bool TryResolveDependency(SyntaxNode node, SemanticModel semanticModel, out ISymbol dependencySymbol, out string dependencyKind)
    {
        dependencySymbol = null!;
        dependencyKind = string.Empty;

        switch (node)
        {
            case UsingDirectiveSyntax usingDirective:
                dependencySymbol = semanticModel.GetSymbolInfo(usingDirective.Name!).Symbol
                    ?? semanticModel.GetTypeInfo(usingDirective.Name!).Type
                    ?? semanticModel.GetSymbolInfo(usingDirective).Symbol!;
                if (dependencySymbol is null)
                {
                    return false;
                }

                dependencyKind = "reference";
                return true;
            case ObjectCreationExpressionSyntax creation:
                dependencySymbol = semanticModel.GetTypeInfo(creation).Type!;
                if (dependencySymbol is null)
                {
                    return false;
                }

                dependencyKind = "creation";
                return true;
            case InvocationExpressionSyntax invocation:
                dependencySymbol = semanticModel.GetSymbolInfo(invocation).Symbol!;
                if (dependencySymbol is null)
                {
                    return false;
                }

                dependencyKind = "invocation";
                return true;
            case BaseTypeSyntax baseType:
                dependencySymbol = semanticModel.GetTypeInfo(baseType.Type).Type!;
                if (dependencySymbol is null)
                {
                    return false;
                }

                dependencyKind = "inheritance";
                return true;
            case AttributeSyntax attribute:
                dependencySymbol = semanticModel.GetTypeInfo(attribute).Type!;
                if (dependencySymbol is null)
                {
                    return false;
                }

                dependencyKind = "attribute";
                return true;
            default:
                return false;
        }
    }

    private static LoadedProject? ResolveTargetProject(
        IReadOnlyDictionary<string, LoadedProject> projectsByAssemblyName,
        IAssemblySymbol? assembly,
        string currentAssemblyName)
    {
        if (assembly is null || string.IsNullOrWhiteSpace(assembly.Name) || string.Equals(assembly.Name, currentAssemblyName, StringComparison.Ordinal))
        {
            return null;
        }

        return projectsByAssemblyName.TryGetValue(assembly.Name, out var targetProject) ? targetProject : null;
    }

    private static async Task<IReadOnlyList<DiagnosticFinding>> AnalyzeComplexityAsync(
        string projectDirectoryPath,
        CancellationToken cancellationToken)
    {
        var results = new List<DiagnosticFinding>();
        var sourceFiles = Directory.EnumerateFiles(projectDirectoryPath, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(sourceFile, cancellationToken);
            foreach (var method in MethodParser.Parse(content))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var complexity = ComplexityCalculator.Calculate(method.Body);
                if (complexity <= CliContract.ComplexityThreshold)
                {
                    continue;
                }

                var location = TextLocator.LineColumn(content, method.NameStartIndex);
                var properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["methodName"] = method.Name,
                    ["complexity"] = complexity.ToString(),
                    ["threshold"] = CliContract.ComplexityThreshold.ToString()
                };

                results.Add(new DiagnosticFinding(
                    CliContract.ComplexityRuleId,
                    "warning",
                    $"Method '{method.Name}' has cyclomatic complexity {complexity} exceeding threshold {CliContract.ComplexityThreshold}.",
                    new SourceLocation(sourceFile, location.Line, location.Column, location.Line, location.Column),
                    properties));
            }
        }

        return results;
    }
}

internal sealed class WorkspaceLoadException(string message, Exception innerException) : Exception(message, innerException);

internal sealed class RuleExecutionException(string message, Exception innerException) : Exception(message, innerException);
