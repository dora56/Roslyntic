namespace Roslyntic.Core;

/// <param name="FilePath">Absolute file path (file:/// URI in SARIF output).</param>
/// <param name="StartLine">1-based line number.</param>
/// <param name="StartColumn">1-based column number.</param>
/// <param name="EndLine">Optional 1-based end line.</param>
/// <param name="EndColumn">Optional 1-based end column.</param>
public record DiagnosticLocation(
    string FilePath,
    int StartLine,
    int StartColumn,
    int? EndLine,
    int? EndColumn);
