using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Xml.Linq;

internal static class WorkspaceProjectLoader
{
    private static readonly object SyncRoot = new();

    public static async Task<IReadOnlyList<LoadedProject>> LoadAsync(string targetPath, CancellationToken cancellationToken)
    {
        EnsureMsBuildRegistered();

        using var workspace = MSBuildWorkspace.Create();
        var fullPath = Path.GetFullPath(targetPath);
        var extension = Path.GetExtension(fullPath);
        Solution solution;
        if (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase))
        {
            solution = await workspace.OpenSolutionAsync(fullPath, cancellationToken: cancellationToken);
        }
        else if (string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
        {
            var slnxDirectory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException($"Could not resolve solution directory: {fullPath}");
            var projectPaths = LoadSlnxProjectPaths(fullPath);
            foreach (var projectPath in projectPaths)
            {
                var projectFullPath = ResolveSlnxProjectPath(slnxDirectory, projectPath);
                await workspace.OpenProjectAsync(projectFullPath, cancellationToken: cancellationToken);
            }

            solution = workspace.CurrentSolution;
        }
        else if (string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var project = await workspace.OpenProjectAsync(fullPath, cancellationToken: cancellationToken);
            solution = project.Solution;
        }
        else
        {
            throw new InvalidOperationException("Only .sln, .slnx, or .csproj inputs are supported.");
        }

        var loadedProjects = new List<LoadedProject>();
        foreach (var project in solution.Projects.OrderBy(item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = await project.GetCompilationAsync(cancellationToken)
                ?? throw new InvalidOperationException($"Compilation could not be created for project: {project.Name}");
            var projectFilePath = project.FilePath
                ?? throw new InvalidOperationException($"Project path is missing for project: {project.Name}");
            var projectDirectoryPath = Path.GetDirectoryName(projectFilePath)
                ?? throw new InvalidOperationException($"Could not resolve project directory: {projectFilePath}");
            var assemblyName = project.AssemblyName
                ?? throw new InvalidOperationException($"Assembly name is missing for project: {project.Name}");

            loadedProjects.Add(new LoadedProject
            {
                Name = project.Name,
                ProjectFilePath = projectFilePath,
                ProjectDirectoryPath = projectDirectoryPath,
                AssemblyName = assemblyName,
                Layer = LayerClassifier.FromProjectName(project.Name),
                Compilation = compilation
            });
        }

        return loadedProjects;
    }

    private static void EnsureMsBuildRegistered()
    {
        lock (SyncRoot)
        {
            if (MSBuildLocator.IsRegistered)
            {
                return;
            }

            MSBuildLocator.RegisterDefaults();
        }
    }

    private static IReadOnlyList<string> LoadSlnxProjectPaths(string slnxPath)
    {
        var document = XDocument.Load(slnxPath);
        var projectPaths = document.Root?
            .Elements("Project")
            .Select(project => project.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray()
            ?? throw new InvalidOperationException($"Could not read projects from solution: {slnxPath}");
        if (projectPaths.Length == 0)
        {
            throw new InvalidOperationException($"No projects found in solution: {slnxPath}");
        }

        return projectPaths;
    }

    private static string ResolveSlnxProjectPath(string slnxDirectory, string projectPath)
    {
        if (Path.IsPathRooted(projectPath))
        {
            throw new InvalidOperationException($"Absolute project path is not allowed in .slnx: {projectPath}");
        }

        var projectFullPath = Path.GetFullPath(Path.Combine(slnxDirectory, projectPath));
        var relativePath = Path.GetRelativePath(slnxDirectory, projectFullPath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"Project path escapes .slnx directory: {projectPath}");
        }

        return projectFullPath;
    }
}
