using System.Text;
using Roslyntic.Tests.TestSupport;

namespace Roslyntic.Tests.Integration;

[Collection("RoslynticApp serial")]
public sealed class LayoutMigrationContractTests
{
    [Fact]
    public void Should_reference_projects_under_src_and_tests_in_solution_definition()
    {
        // Given
        var repositoryRoot = CliHarness.RepositoryRootPath;
        var solutionPath = Path.Combine(repositoryRoot, "Roslyntic.slnx");

        // When
        var solutionContent = File.ReadAllText(solutionPath, Encoding.UTF8);

        // Then
        TestAssertions.True(
            solutionContent.Contains("<Project Path=\"src/Roslyntic.Cli/Roslyntic.Cli.csproj\" />", StringComparison.Ordinal),
            "Expected Roslyntic.slnx to reference src/Roslyntic.Cli/Roslyntic.Cli.csproj.");
        TestAssertions.True(
            solutionContent.Contains("<Project Path=\"tests/Roslyntic.Tests/Roslyntic.Tests.csproj\" />", StringComparison.Ordinal),
            "Expected Roslyntic.slnx to reference tests/Roslyntic.Tests/Roslyntic.Tests.csproj.");
    }

    [Fact]
    public void Should_reference_cli_project_from_tests_project_using_migrated_relative_path()
    {
        // Given
        var repositoryRoot = CliHarness.RepositoryRootPath;
        var testsProjectPath = Path.Combine(repositoryRoot, "tests", "Roslyntic.Tests", "Roslyntic.Tests.csproj");

        // When
        var testsProjectContent = File.ReadAllText(testsProjectPath, Encoding.UTF8);

        // Then
        TestAssertions.True(
            testsProjectContent.Contains("<ProjectReference Include=\"../../src/Roslyntic.Cli/Roslyntic.Cli.csproj\" />", StringComparison.Ordinal),
            "Expected tests project to reference ../../src/Roslyntic.Cli/Roslyntic.Cli.csproj.");
    }

    [Fact]
    public void Should_run_check_via_migrated_cli_project_path()
    {
        // Given
        var repositoryRoot = CliHarness.RepositoryRootPath;
        var cliProjectPath = Path.Combine(repositoryRoot, "src", "Roslyntic.Cli", "Roslyntic.Cli.csproj");
        var solutionPath = Path.Combine(repositoryRoot, "Roslyntic.slnx");

        // When
        var result = CliHarness.RunDotnet($"run --no-build --project \"{cliProjectPath}\" -- check \"{solutionPath}\"");

        // Then
        TestAssertions.Equal(1, result.ExitCode, "Expected exit code 1 when findings exist.");
        var _ = JsonDocument.Parse(result.StdOut);
        TestAssertions.True(result.StdErr.Contains("event=run_end", StringComparison.Ordinal), "Expected STDERR to contain run summary event.");
        TestAssertions.True(result.StdErr.Contains("run.durMs=", StringComparison.Ordinal), "Expected STDERR to contain timing summary.");
    }
}
