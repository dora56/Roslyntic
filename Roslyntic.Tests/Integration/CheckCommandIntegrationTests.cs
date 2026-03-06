using System.Text.Json;
using Xunit;

namespace Roslyntic.Tests.Integration;

/// <summary>
/// End-to-end integration tests for `roslyntic check samples/Samples.sln`.
/// These tests exercise the full pipeline:
///   workspace load → rule analysis → diagnostic sorting → SARIF serialization → STDOUT.
/// </summary>
public class CheckCommandIntegrationTests
{
    // Resolve paths relative to the repository root (two levels up from test output dir)
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static readonly string SamplesSln =
        Path.Combine(RepoRoot, "samples", "Samples.sln");

    private static readonly string CliBin =
        Path.Combine(RepoRoot, "Roslyntic.Cli", "bin", "Debug", "net10.0", "Roslyntic.Cli");

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<(string stdout, string stderr, int exitCode)> RunCliAsync(
        params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName               = CliBin,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start CLI process.");

        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (stdout, stderr, proc.ExitCode);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_SampleSolution_ExitsWithCode1WhenFindingsExist()
    {
        // Given: the sample solution intentionally contains violations
        // When
        var (_, _, exitCode) = await RunCliAsync("check", SamplesSln);

        // Then: exit 1 = findings found (not 0=clean, not 2=tool error)
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Check_SampleSolution_StdoutIsValidJson()
    {
        // Given / When
        var (stdout, _, _) = await RunCliAsync("check", SamplesSln);

        // Then: STDOUT must be parseable JSON
        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task Check_SampleSolution_OutputIsSarif21()
    {
        // Given / When
        var (stdout, _, _) = await RunCliAsync("check", SamplesSln);

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        // Then: SARIF 2.1.0 envelope
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        var runs = root.GetProperty("runs");
        Assert.Equal(1, runs.GetArrayLength());
    }

    [Fact]
    public async Task Check_SampleSolution_ContainsAgArch0001Findings()
    {
        // Given: Samples.UI references Infrastructure/Domain directly (intentional violation)
        // When
        var (stdout, _, _) = await RunCliAsync("check", SamplesSln);

        using var doc = JsonDocument.Parse(stdout);
        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");

        var ruleIds = results.EnumerateArray()
            .Select(r => r.GetProperty("ruleId").GetString())
            .ToList();

        // Then: layer violation rule must fire
        Assert.Contains("AGARCH0001", ruleIds);
    }

    [Fact]
    public async Task Check_SampleSolution_ContainsAgComp0001Findings()
    {
        // Given: the sample solution has a method with complexity > 15 (intentional)
        // When
        var (stdout, _, _) = await RunCliAsync("check", SamplesSln);

        using var doc = JsonDocument.Parse(stdout);
        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");

        var ruleIds = results.EnumerateArray()
            .Select(r => r.GetProperty("ruleId").GetString())
            .ToList();

        // Then
        Assert.Contains("AGCOMP0001", ruleIds);
    }

    [Fact]
    public async Task Check_SampleSolution_ResultsAreBitwiseIdenticalAcrossRuns()
    {
        // Given: determinism requirement — same input must produce same SARIF
        // When: two independent runs
        var (first,  _, _) = await RunCliAsync("check", SamplesSln);
        var (second, _, _) = await RunCliAsync("check", SamplesSln);

        // Then: byte-identical
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Check_SampleSolution_ResultsOrderedByPathThenLineThenColumnThenRuleId()
    {
        // Given / When
        var (stdout, _, _) = await RunCliAsync("check", SamplesSln);

        using var doc = JsonDocument.Parse(stdout);
        var results = doc.RootElement.GetProperty("runs")[0].GetProperty("results")
            .EnumerateArray()
            .Select(r =>
            {
                var physLoc = r.GetProperty("locations")[0].GetProperty("physicalLocation");
                var region  = physLoc.GetProperty("region");
                return (
                    uri:  physLoc.GetProperty("artifactLocation").GetProperty("uri").GetString()!,
                    line: region.GetProperty("startLine").GetInt32(),
                    col:  region.GetProperty("startColumn").GetInt32(),
                    rule: r.GetProperty("ruleId").GetString()!
                );
            })
            .ToList();

        // Then: each entry must be >= its predecessor in the canonical sort order
        for (var i = 1; i < results.Count; i++)
        {
            var prev = results[i - 1];
            var curr = results[i];

            var uriCmp = StringComparer.Ordinal.Compare(prev.uri, curr.uri);
            if (uriCmp < 0) continue;
            Assert.Equal(0, uriCmp);

            if (curr.line > prev.line) continue;
            Assert.True(curr.line >= prev.line, $"Line regression at index {i}");

            if (curr.col > prev.col) continue;
            Assert.True(curr.col >= prev.col, $"Column regression at index {i}");

            Assert.True(
                StringComparer.Ordinal.Compare(prev.rule, curr.rule) <= 0,
                $"RuleId regression at index {i}");
        }
    }

    [Fact]
    public async Task Check_SampleSolution_StderrDoesNotPolluteSarif()
    {
        // Given / When
        var (stdout, stderr, _) = await RunCliAsync("check", SamplesSln);

        // Then: SARIF output goes to STDOUT only; STDERR must not contain JSON
        Assert.DoesNotContain("{\"version\"", stderr, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal("2.1.0", doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Check_NonExistentPath_ExitsWithCode2()
    {
        // Given: a path that does not exist
        // When
        var (_, _, exitCode) = await RunCliAsync("check", "/nonexistent/does-not-exist.sln");

        // Then: tool execution failure → exit code 2
        Assert.Equal(2, exitCode);
    }
}
