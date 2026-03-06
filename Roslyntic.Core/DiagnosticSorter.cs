namespace Roslyntic.Core;

/// <summary>
/// Sorts diagnostics deterministically: path → startLine → startColumn → ruleId.
/// Uses <see cref="StringComparer.Ordinal"/> for all string comparisons.
/// </summary>
public static class DiagnosticSorter
{
    public static IEnumerable<Diagnostic> Sort(IEnumerable<Diagnostic> diagnostics)
        => diagnostics
            .OrderBy(d => d.Location.FilePath, StringComparer.Ordinal)
            .ThenBy(d => d.Location.StartLine)
            .ThenBy(d => d.Location.StartColumn)
            .ThenBy(d => d.RuleId, StringComparer.Ordinal);
}
