namespace Roslyntic.Core;

public record RuleMetadata(
    string Id,
    string Title,
    string Description,
    string Category,
    DiagnosticLevel DefaultLevel,
    string? HelpUri);
