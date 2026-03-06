namespace Roslyntic.Core;

public record Diagnostic(
    string RuleId,
    DiagnosticLevel Level,
    string Message,
    DiagnosticLocation Location,
    IReadOnlyDictionary<string, string> Properties);
