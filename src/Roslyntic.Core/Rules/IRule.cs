using Microsoft.CodeAnalysis;

namespace Roslyntic.Core.Rules;

public interface IRule
{
    RuleMetadata Metadata { get; }

    IAsyncEnumerable<Diagnostic> AnalyzeAsync(
        Project project,
        Compilation compilation,
        CancellationToken cancellationToken);
}
