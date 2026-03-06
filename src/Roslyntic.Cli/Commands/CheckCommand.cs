using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using Roslyntic.Core;
using Roslyntic.Core.Logging;
using Roslyntic.Core.Rules;
using Roslyntic.Rules.Arch;
using Roslyntic.Rules.Complexity;
using Roslyntic.Sarif;

namespace Roslyntic.Cli.Commands;

internal static class CheckCommand
{
    internal const string ToolVersion = "1.0.0";

    internal static async Task<int> RunAsync(
        string path,
        IAnalysisLogger logger,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            logger.LogError($"Path not found: {path}");
            return 2;
        }

        try
        {
            // MSBuildLocator must be called before any MSBuild types are loaded.
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to register MSBuild: {ex.Message}");
            return 2;
        }

        var rules = new IRule[]
        {
            new LayerViolationRule(),
            new CyclomaticComplexityRule(),
        };

        List<RoslynticDiagnostic> allDiagnostics;

        try
        {
            using var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += (_, e)
                => logger.LogWarning($"Workspace: {e.Diagnostic.Message}");

            var ext = Path.GetExtension(path).ToLowerInvariant();
            IReadOnlyList<Microsoft.CodeAnalysis.Project> projectsToAnalyze;

            if (ext is ".sln" or ".slnx")
            {
                var solution = await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                projectsToAnalyze = solution.Projects.ToList();
            }
            else if (ext == ".csproj")
            {
                var project = await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                projectsToAnalyze = [project];
            }
            else
            {
                logger.LogError($"Unsupported file type: {ext}. Expected .sln, .slnx, or .csproj.");
                return 2;
            }

            allDiagnostics = [];

            foreach (var project in projectsToAnalyze)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                if (compilation is null)
                {
                    logger.LogWarning($"Could not compile: {project.Name}");
                    continue;
                }

                foreach (var rule in rules)
                {
                    await foreach (var diagnostic in rule.AnalyzeAsync(project, compilation, cancellationToken)
                                       .ConfigureAwait(false))
                    {
                        allDiagnostics.Add(diagnostic);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Analysis failed: {ex.Message}");
            return 2;
        }

        try
        {
            var sorted    = DiagnosticSorter.Sort(allDiagnostics).ToList();
            var rulesMeta = rules.Select(r => r.Metadata);
            var sarif     = SarifSerializer.Serialize(sorted, rulesMeta, ToolVersion);

            // STDOUT is machine-readable only.
            Console.WriteLine(sarif);

            return sorted.Count > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            logger.LogError($"Serialization failed: {ex.Message}");
            return 2;
        }
    }
}
