using System.Text;
using Roslyntic.Tests.TestSupport;

namespace Roslyntic.Tests.TestSupport;

internal static class CliHarness
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(CliContract.AnalysisTimeoutSeconds);
    private static readonly string RepositoryRoot = ResolveRepositoryRoot();
    public static string RepositoryRootPath => RepositoryRoot;

    public static (int ExitCode, string StdOut, string StdErr) RunDotnet(string arguments)
    {
        return RunProcess(new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }).GetAwaiter().GetResult();
    }

    public static (int ExitCode, string StdOut, string StdErr) RunCheck(string targetPath, params string[] additionalArguments)
    {
        var cliDllPath = Path.Combine(RepositoryRoot, "src", "Roslyntic.Cli", "bin", "Debug", "net10.0", "Roslyntic.Cli.dll");
        var extra = additionalArguments.Length == 0
            ? string.Empty
            : " " + string.Join(" ", additionalArguments.Select(Quote));
        return RunDotnet($"\"{cliDllPath}\" check \"{targetPath}\"{extra}");
    }

    public static string CreateSampleSolution(string solutionFormat = "sln")
    {
        var root = Path.Combine(Path.GetTempPath(), $"roslyntic-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        if (!string.Equals(solutionFormat, "sln", StringComparison.Ordinal)
            && !string.Equals(solutionFormat, "slnx", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported solution format: {solutionFormat}");
        }

        Run("dotnet", $"new sln --name Samples --format {solutionFormat}", root);
        var solutionPath = Path.Combine(root, $"Samples.{solutionFormat}");
        if (!File.Exists(solutionPath))
        {
            throw new InvalidOperationException($"Solution file was not created under {root}.");
        }

        var solutionFileName = Path.GetFileName(solutionPath);
        Run("dotnet", "new classlib --name Samples.Domain", root);
        Run("dotnet", "new classlib --name Samples.Application", root);
        Run("dotnet", "new classlib --name Samples.Infrastructure", root);
        Run("dotnet", "new classlib --name Samples.UI", root);

        Run("dotnet", $"sln {solutionFileName} add Samples.Domain/Samples.Domain.csproj", root);
        Run("dotnet", $"sln {solutionFileName} add Samples.Application/Samples.Application.csproj", root);
        Run("dotnet", $"sln {solutionFileName} add Samples.Infrastructure/Samples.Infrastructure.csproj", root);
        Run("dotnet", $"sln {solutionFileName} add Samples.UI/Samples.UI.csproj", root);
        Run("dotnet", "add Samples.Application/Samples.Application.csproj reference Samples.Domain/Samples.Domain.csproj", root);
        Run("dotnet", "add Samples.Infrastructure/Samples.Infrastructure.csproj reference Samples.Domain/Samples.Domain.csproj", root);
        Run("dotnet", "add Samples.UI/Samples.UI.csproj reference Samples.Application/Samples.Application.csproj", root);
        Run("dotnet", "add Samples.UI/Samples.UI.csproj reference Samples.Infrastructure/Samples.Infrastructure.csproj", root);

        File.WriteAllText(Path.Combine(root, "Samples.UI", "Violation.cs"), """
namespace Samples.UI;

using Samples.Infrastructure;

public class Violation
{
    public int TooComplex(int x)
    {
        var y = 0;
        if (x > 0 && x < 100) y++;
        for (var i = 0; i < x; i++) { y += i; }
        while (y < 1000) { y++; if (y % 2 == 0) y += 3; }
        do { y--; } while (y > 10);
        switch (x)
        {
            case 1: y++; break;
            case 2: y++; break;
            case 3: y++; break;
            case 4: y++; break;
            case 5: y++; break;
            case 6: y++; break;
            case 7: y++; break;
            case 8: y++; break;
            default: y += x > 5 || x < -5 ? 10 : 1; break;
        }
        return y;
    }
}
""", Encoding.UTF8);

        return solutionPath;
    }

    private static void Run(string fileName, string arguments, string workingDirectory)
    {
        var (exitCode, output, error) = RunProcess(new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }).GetAwaiter().GetResult();

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Command failed: {fileName} {arguments}\n{output}\n{error}");
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcess(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        TestAssertions.NotNull(process, $"Command start failed: {startInfo.FileName} {startInfo.Arguments}");

        var waitForExitTask = process!.WaitForExitAsync();
        var completedTask = await Task.WhenAny(waitForExitTask, Task.Delay(ProcessTimeout));
        if (completedTask != waitForExitTask)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            return (CliContract.ExecutionErrorExitCode, string.Empty, $"Command timed out after {ProcessTimeout.TotalSeconds:0.###} seconds: {startInfo.FileName} {startInfo.Arguments}");
        }

        var stdOut = await process.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();
        return (process.ExitCode, stdOut, stdErr);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Roslyntic.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root containing Roslyntic.slnx was not found.");
    }
}
