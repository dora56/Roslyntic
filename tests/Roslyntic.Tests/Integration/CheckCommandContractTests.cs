using Roslyntic.Tests.TestSupport;

namespace Roslyntic.Tests.Integration;

[Collection("RoslynticApp serial")]
public sealed class CheckCommandContractTests
{
    [Fact]
    public void Should_return_1_and_stable_stdout_when_findings_exist()
    {
        var solutionPath = CliHarness.CreateSampleSolution();
        var first = CliHarness.RunCheck(solutionPath);
        var second = CliHarness.RunCheck(solutionPath);
        TestAssertions.Equal(1, first.ExitCode, "Expected exit code 1 when findings exist.");
        TestAssertions.Equal(first.StdOut, second.StdOut, "Expected deterministic output for identical input.");
        AssertRunSummary(first.StdErr, CliContract.FindingsExitCode);
        AssertRunSummary(second.StdErr, CliContract.FindingsExitCode);
    }

    [Fact]
    public void Should_accept_slnx_input_and_return_1_when_findings_exist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var solutionPath = Path.Combine(repositoryRoot, "Roslyntic.slnx");
        var first = CliHarness.RunCheck(solutionPath);
        var second = CliHarness.RunCheck(solutionPath);
        TestAssertions.Equal(1, first.ExitCode, "Expected exit code 1 when findings exist for .slnx input.");
        TestAssertions.Equal(first.StdOut, second.StdOut, "Expected deterministic output for identical .slnx input.");
        AssertRunSummary(first.StdErr, CliContract.FindingsExitCode);
        AssertRunSummary(second.StdErr, CliContract.FindingsExitCode);
    }

    [Fact]
    public void Should_return_0_when_no_findings_exist()
    {
        var root = Path.Combine(Path.GetTempPath(), $"roslyntic-clean-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "Clean.csproj");
        File.WriteAllText(projectPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""");
        File.WriteAllText(Path.Combine(root, "Class1.cs"), "namespace Clean; public class Class1 { public int Value() => 1; }");
        var result = CliHarness.RunCheck(projectPath);
        TestAssertions.Equal(0, result.ExitCode, "Expected exit code 0 when no findings exist.");
        AssertRunSummary(result.StdErr, CliContract.NoFindingsExitCode);
    }

    [Fact]
    public void Should_return_2_for_execution_error()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.sln");
        var result = CliHarness.RunCheck(invalidPath);
        TestAssertions.Equal(2, result.ExitCode, "Expected exit code 2 for execution error.");
        TestAssertions.Equal(string.Empty, result.StdOut, "Expected STDOUT to remain empty on execution error.");
        TestAssertions.True(result.StdErr.Length > 0, "Expected STDERR to contain human readable error details.");
    }

    [Fact]
    public void Should_reject_absolute_project_paths_in_slnx()
    {
        var root = Path.Combine(Path.GetTempPath(), $"roslyntic-abs-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var repositoryRoot = FindRepositoryRoot();
        var absoluteProjectPath = Path.Combine(repositoryRoot, "src", "Roslyntic.Cli", "Roslyntic.Cli.csproj");
        var slnxPath = Path.Combine(root, "Invalid.slnx");
        File.WriteAllText(slnxPath, $$"""
<Solution>
  <Project Path="{{absoluteProjectPath}}" />
</Solution>
""");

        var result = CliHarness.RunCheck(slnxPath);

        TestAssertions.Equal(2, result.ExitCode, "Expected exit code 2 when .slnx contains absolute project paths.");
        TestAssertions.Equal(string.Empty, result.StdOut, "Expected STDOUT to remain empty for invalid .slnx paths.");
        TestAssertions.True(result.StdErr.Contains("Execution failed", StringComparison.Ordinal), "Expected STDERR to contain execution failure message.");
    }

    [Fact]
    public void Should_reject_traversal_project_paths_in_slnx()
    {
        var root = Path.Combine(Path.GetTempPath(), $"roslyntic-relative-path-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(nested);
        var slnxPath = Path.Combine(nested, "Invalid.slnx");
        File.WriteAllText(slnxPath, """
<Solution>
  <Project Path="../outside/Outside.csproj" />
</Solution>
""");

        var result = CliHarness.RunCheck(slnxPath);

        TestAssertions.Equal(2, result.ExitCode, "Expected exit code 2 when .slnx project path escapes solution directory.");
        TestAssertions.Equal(string.Empty, result.StdOut, "Expected STDOUT to remain empty for traversal project paths.");
        TestAssertions.True(result.StdErr.Contains("Execution failed", StringComparison.Ordinal), "Expected STDERR to contain execution failure message.");
    }

    [Fact]
    public void Should_write_machine_output_to_stdout_and_human_messages_to_stderr()
    {
        var solutionPath = CliHarness.CreateSampleSolution();
        var result = CliHarness.RunCheck(solutionPath);
        var _ = JsonDocument.Parse(result.StdOut);
        TestAssertions.True(!result.StdOut.Contains("error", StringComparison.OrdinalIgnoreCase), "Expected STDOUT to be machine readable output only.");
        AssertRunSummary(result.StdErr, CliContract.FindingsExitCode);
    }

    [Fact]
    public void Should_emit_json_output_when_format_json_is_specified()
    {
        var solutionPath = CliHarness.CreateSampleSolution();
        var result = CliHarness.RunCheck(solutionPath, "--format", "json");
        TestAssertions.Equal(1, result.ExitCode, "Expected exit code 1 when findings exist in json mode.");
        var payload = JsonDocument.Parse(result.StdOut);
        var root = payload.RootElement;
        var rules = root.GetProperty("rules").EnumerateArray().ToArray();
        TestAssertions.True(rules.Length > 0, "Expected rules in json output.");
        TestAssertions.NotNull(rules[0].GetProperty("id").GetString(), "Expected rule id in json output.");
        TestAssertions.True(root.GetProperty("diagnostics").EnumerateArray().Any(), "Expected diagnostics in json output.");
        AssertRunSummary(result.StdErr, CliContract.FindingsExitCode);
    }

    [Fact]
    public void Should_return_2_and_stderr_message_when_format_is_invalid()
    {
        var solutionPath = CliHarness.CreateSampleSolution();
        var result = CliHarness.RunCheck(solutionPath, "--format", "yaml");
        TestAssertions.Equal(2, result.ExitCode, "Expected exit code 2 for unsupported format.");
        TestAssertions.Equal(string.Empty, result.StdOut, "Expected STDOUT to remain empty for unsupported format.");
        TestAssertions.True(result.StdErr.Contains("Execution failed", StringComparison.Ordinal), "Expected STDERR to contain execution failure message.");
    }

    [Fact]
    public async Task Should_return_2_and_timeout_message_when_analysis_times_out()
    {
        var root = Path.Combine(Path.GetTempPath(), $"roslyntic-timeout-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "Timeout.csproj");
        File.WriteAllText(projectPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""");

        var originalAnalyzer = RoslynticApp.AnalyzeAsyncCore;
        var originalTimeout = RoslynticApp.AnalysisTimeout;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdOut = new StringWriter();
        var stdErr = new StringWriter();

        RoslynticApp.AnalyzeAsyncCore = async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new AnalysisExecutionResult(Array.Empty<DiagnosticFinding>(), new AnalysisMetrics(0, 0, 0, 0, CliContract.RulesExecutedCount));
        };
        RoslynticApp.AnalysisTimeout = TimeSpan.FromMilliseconds(20);
        Console.SetOut(stdOut);
        Console.SetError(stdErr);

        try
        {
            var exitCode = await RoslynticApp.RunAsync(["check", projectPath]);
            TestAssertions.Equal(2, exitCode, "Expected exit code 2 on analysis timeout.");
            TestAssertions.Equal(string.Empty, stdOut.ToString(), "Expected STDOUT to stay empty on timeout.");
            TestAssertions.True(stdErr.ToString().Contains("timed out", StringComparison.OrdinalIgnoreCase), "Expected STDERR to contain timeout message.");
            AssertRunSummary(stdErr.ToString(), CliContract.ExecutionErrorExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            RoslynticApp.AnalyzeAsyncCore = originalAnalyzer;
            RoslynticApp.AnalysisTimeout = originalTimeout;
        }
    }

    [Fact]
    public async Task Should_emit_workspace_load_failure_category_and_timing_keys()
    {
        var root = Path.Combine(Path.GetTempPath(), $"roslyntic-workspace-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "WorkspaceFailure.csproj");
        File.WriteAllText(projectPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
""");

        var originalAnalyzer = RoslynticApp.AnalyzeAsyncCore;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdOut = new StringWriter();
        var stdErr = new StringWriter();

        RoslynticApp.AnalyzeAsyncCore = (_, _) => throw new WorkspaceLoadException("Workspace loading failed.", new InvalidOperationException("load"));
        Console.SetOut(stdOut);
        Console.SetError(stdErr);

        try
        {
            var exitCode = await RoslynticApp.RunAsync(["check", projectPath]);
            TestAssertions.Equal(CliContract.ExecutionErrorExitCode, exitCode, "Expected exit code 2 on workspace load failure.");
            TestAssertions.Equal(string.Empty, stdOut.ToString(), "Expected STDOUT to remain empty on workspace load failure.");
            TestAssertions.True(stdErr.ToString().Contains("error.category=workspace_load_failure", StringComparison.Ordinal), "Expected workspace load failure category.");
            AssertRunSummary(stdErr.ToString(), CliContract.ExecutionErrorExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            RoslynticApp.AnalyzeAsyncCore = originalAnalyzer;
        }
    }

    [Fact]
    public async Task Should_emit_rule_execution_failure_category_and_timing_keys()
    {
        var root = Path.Combine(Path.GetTempPath(), $"roslyntic-rule-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "RuleFailure.csproj");
        File.WriteAllText(projectPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
""");

        var originalAnalyzer = RoslynticApp.AnalyzeAsyncCore;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdOut = new StringWriter();
        var stdErr = new StringWriter();

        RoslynticApp.AnalyzeAsyncCore = (_, _) => throw new RuleExecutionException("Rule execution failed.", new InvalidOperationException("rule"));
        Console.SetOut(stdOut);
        Console.SetError(stdErr);

        try
        {
            var exitCode = await RoslynticApp.RunAsync(["check", projectPath]);
            TestAssertions.Equal(CliContract.ExecutionErrorExitCode, exitCode, "Expected exit code 2 on rule execution failure.");
            TestAssertions.Equal(string.Empty, stdOut.ToString(), "Expected STDOUT to remain empty on rule execution failure.");
            TestAssertions.True(stdErr.ToString().Contains("error.category=rule_execution_failure", StringComparison.Ordinal), "Expected rule execution failure category.");
            AssertRunSummary(stdErr.ToString(), CliContract.ExecutionErrorExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            RoslynticApp.AnalyzeAsyncCore = originalAnalyzer;
        }
    }

    [Fact]
    public async Task Should_emit_unexpected_exception_category_and_timing_keys()
    {
        var root = Path.Combine(Path.GetTempPath(), $"roslyntic-unexpected-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "Unexpected.csproj");
        File.WriteAllText(projectPath, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
""");

        var originalAnalyzer = RoslynticApp.AnalyzeAsyncCore;
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdOut = new StringWriter();
        var stdErr = new StringWriter();

        RoslynticApp.AnalyzeAsyncCore = (_, _) => throw new InvalidOperationException("unexpected");
        Console.SetOut(stdOut);
        Console.SetError(stdErr);

        try
        {
            var exitCode = await RoslynticApp.RunAsync(["check", projectPath]);
            TestAssertions.Equal(CliContract.ExecutionErrorExitCode, exitCode, "Expected exit code 2 on unexpected exception.");
            TestAssertions.Equal(string.Empty, stdOut.ToString(), "Expected STDOUT to remain empty on unexpected exception.");
            TestAssertions.True(stdErr.ToString().Contains("error.category=unexpected_exception", StringComparison.Ordinal), "Expected unexpected exception category.");
            AssertRunSummary(stdErr.ToString(), CliContract.ExecutionErrorExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            RoslynticApp.AnalyzeAsyncCore = originalAnalyzer;
        }
    }

    private static string FindRepositoryRoot()
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

    private static void AssertRunSummary(string stdErr, int expectedExitCode)
    {
        TestAssertions.True(stdErr.Contains("event=run_end", StringComparison.Ordinal), "Expected run_end event in STDERR.");
        TestAssertions.True(stdErr.Contains("run.durMs=", StringComparison.Ordinal), "Expected run duration key in STDERR.");
        TestAssertions.True(stdErr.Contains("workspace_load.durMs=", StringComparison.Ordinal), "Expected workspace load duration key in STDERR.");
        TestAssertions.True(stdErr.Contains("rules.durMs=", StringComparison.Ordinal), "Expected rules duration key in STDERR.");
        TestAssertions.True(stdErr.Contains("output_write.durMs=", StringComparison.Ordinal), "Expected output write duration key in STDERR.");
        TestAssertions.True(stdErr.Contains("projects=", StringComparison.Ordinal), "Expected projects count key in STDERR.");
        TestAssertions.True(stdErr.Contains("documents=", StringComparison.Ordinal), "Expected documents count key in STDERR.");
        TestAssertions.True(stdErr.Contains("rulesExecuted=", StringComparison.Ordinal), "Expected rules executed key in STDERR.");
        TestAssertions.True(stdErr.Contains("findings.error=", StringComparison.Ordinal), "Expected findings.error key in STDERR.");
        TestAssertions.True(stdErr.Contains("findings.warning=", StringComparison.Ordinal), "Expected findings.warning key in STDERR.");
        TestAssertions.True(stdErr.Contains("findings.note=", StringComparison.Ordinal), "Expected findings.note key in STDERR.");
        TestAssertions.True(stdErr.Contains($"exitCode={expectedExitCode}", StringComparison.Ordinal), "Expected exit code key in STDERR.");
    }
}
