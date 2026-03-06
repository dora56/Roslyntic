using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyntic.Analysis;
using Roslyntic.Core;
using Roslyntic.Core.Rules;

namespace Roslyntic.Rules.Complexity;

public sealed class CyclomaticComplexityRule : IRule
{
    public const string RuleId = "AGCOMP0001";
    public const int DefaultThreshold = 15;

    private readonly int _threshold;

    public CyclomaticComplexityRule(int threshold = DefaultThreshold)
    {
        _threshold = threshold;
    }

    public RuleMetadata Metadata { get; } = new(
        Id: RuleId,
        Title: "Cyclomatic complexity threshold exceeded",
        Description: "Reports methods whose cyclomatic complexity exceeds the configured threshold.",
        Category: "Complexity",
        DefaultLevel: DiagnosticLevel.Warning,
        HelpUri: null);

    public async IAsyncEnumerable<RoslynticDiagnostic> AnalyzeAsync(
        Project project,
        Compilation compilation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var document in project.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
                continue;

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var complexity = CyclomaticComplexityCalculator.Calculate(method);
                if (complexity <= _threshold)
                    continue;

                var span = method.Identifier.GetLocation().GetLineSpan();
                var startLine = span.StartLinePosition.Line + 1;
                var startCol = span.StartLinePosition.Character + 1;

                var location = new DiagnosticLocation(
                    FilePath: document.FilePath ?? document.Name,
                    StartLine: startLine,
                    StartColumn: startCol,
                    EndLine: null,
                    EndColumn: null);

                var props = new Dictionary<string, string>
                {
                    ["methodName"] = method.Identifier.Text,
                    ["complexity"] = complexity.ToString(),
                    ["threshold"]  = _threshold.ToString(),
                };

                yield return new RoslynticDiagnostic(
                    RuleId: RuleId,
                    Level: DiagnosticLevel.Warning,
                    Message: $"Method '{method.Identifier.Text}' has cyclomatic complexity {complexity} (threshold {_threshold}).",
                    Location: location,
                    Properties: props);
            }
        }
    }
}
