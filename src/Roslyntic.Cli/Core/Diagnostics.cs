internal sealed record SourceLocation(string Path, int StartLine, int StartColumn, int EndLine, int EndColumn);

internal sealed record DiagnosticFinding(
    string RuleId,
    string Level,
    string Message,
    SourceLocation Location,
    IReadOnlyDictionary<string, string> Properties);

internal sealed record AnalysisMetrics(
    long WorkspaceLoadDurationMs,
    long RulesDurationMs,
    int Projects,
    int Documents,
    int RulesExecuted);

internal sealed record AnalysisExecutionResult(
    IReadOnlyList<DiagnosticFinding> Findings,
    AnalysisMetrics Metrics);

internal static class PathNormalizer
{
    public static string Normalize(string path) => Path.GetFullPath(path).Replace('\\', '/');
}

internal static class DiagnosticSorter
{
    public static IReadOnlyList<DiagnosticFinding> Sort(IReadOnlyList<DiagnosticFinding> diagnostics)
    {
        return diagnostics
            .OrderBy(d => PathNormalizer.Normalize(d.Location.Path), StringComparer.Ordinal)
            .ThenBy(d => d.Location.StartLine)
            .ThenBy(d => d.Location.StartColumn)
            .ThenBy(d => d.RuleId, StringComparer.Ordinal)
            .ToArray();
    }
}
