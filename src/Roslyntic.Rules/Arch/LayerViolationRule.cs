using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyntic.Core;
using Roslyntic.Core.Rules;

namespace Roslyntic.Rules.Arch;

/// <summary>
/// Detects forbidden layer dependencies:
///   UI → Infrastructure, UI → Domain, Domain → Infrastructure
///
/// Reports the using directive syntax node that introduces the dependency.
/// </summary>
public sealed class LayerViolationRule : IRule
{
    public const string RuleId = "AGARCH0001";

    // (from, to) pairs that are forbidden
    private static readonly (Layer From, Layer To)[] ForbiddenEdges =
    [
        (Layer.UI, Layer.Infrastructure),
        (Layer.UI, Layer.Domain),
        (Layer.Domain, Layer.Infrastructure),
    ];

    public RuleMetadata Metadata { get; } = new(
        Id: RuleId,
        Title: "Layer violation",
        Description: "Detects forbidden dependencies between architectural layers.",
        Category: "Architecture",
        DefaultLevel: DiagnosticLevel.Warning,
        HelpUri: null);

    public async IAsyncEnumerable<RoslynticDiagnostic> AnalyzeAsync(
        Project project,
        Compilation compilation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fromLayer = LayerClassifier.Classify(project.AssemblyName);
        if (fromLayer is null)
            yield break;

        // Quick exit: determine which referenced assembly names are forbidden before walking syntax.
        var forbiddenAssemblyNames = compilation.ReferencedAssemblyNames
            .Where(r =>
            {
                var toLayer = LayerClassifier.Classify(r.Name);
                return toLayer is not null && IsForbidden(fromLayer.Value, toLayer.Value);
            })
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (forbiddenAssemblyNames.Count == 0)
            yield break;

        foreach (var document in project.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null) continue;

            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (model is null) continue;

            foreach (var usingDir in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var referencedAssembly = ResolveAssemblyName(model, usingDir, cancellationToken);
                if (referencedAssembly is null || !forbiddenAssemblyNames.Contains(referencedAssembly))
                    continue;

                var toLayer = LayerClassifier.Classify(referencedAssembly)!.Value;

                var span = usingDir.GetLocation().GetLineSpan();
                var location = new DiagnosticLocation(
                    FilePath: document.FilePath ?? document.Name,
                    StartLine: span.StartLinePosition.Line + 1,
                    StartColumn: span.StartLinePosition.Character + 1,
                    EndLine: span.EndLinePosition.Line + 1,
                    EndColumn: span.EndLinePosition.Character + 1);

                var props = new Dictionary<string, string>
                {
                    ["fromLayer"]      = fromLayer.Value.ToString(),
                    ["toLayer"]        = toLayer.ToString(),
                    ["fromSymbol"]     = project.AssemblyName,
                    ["toSymbol"]       = referencedAssembly,
                    ["dependencyKind"] = "reference",
                };

                yield return new RoslynticDiagnostic(
                    RuleId: RuleId,
                    Level: DiagnosticLevel.Warning,
                    Message: $"'{project.AssemblyName}' ({fromLayer.Value}) must not reference '{referencedAssembly}' ({toLayer}) via using directive.",
                    Location: location,
                    Properties: props);
            }
        }
    }

    private static string? ResolveAssemblyName(
        SemanticModel model,
        UsingDirectiveSyntax usingDir,
        CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(usingDir.NamespaceOrType, cancellationToken).Symbol;
        if (symbol is null)
            return null;

        if (symbol is INamespaceSymbol ns)
        {
            // Constituent namespaces each belong to their own assembly.
            foreach (var constituent in ns.ConstituentNamespaces)
            {
                var assemblyName = constituent.ContainingModule?.ContainingAssembly?.Name;
                if (assemblyName is not null)
                    return assemblyName;
            }
            return null;
        }

        if (symbol is INamedTypeSymbol type)
            return type.ContainingAssembly?.Name;

        return null;
    }

    private static bool IsForbidden(Layer from, Layer to)
        => ForbiddenEdges.Any(e => e.From == from && e.To == to);
}
