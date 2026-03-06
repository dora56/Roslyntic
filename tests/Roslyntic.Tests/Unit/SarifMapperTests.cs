using Roslyntic.Tests.TestSupport;

namespace Roslyntic.Tests.Unit;

public sealed class SarifMapperTests
{
    [Fact]
    public void Should_emit_minimal_required_SARIF_2_1_0_fields()
    {
        var solutionPath = CliHarness.CreateSampleSolution();
        var (_, stdOut, _) = CliHarness.RunCheck(solutionPath);
        var sarif = SarifReader.Parse(stdOut);
        TestAssertions.Equal("2.1.0", sarif.GetProperty("version").GetString(), "Expected SARIF version 2.1.0.");
        var run = sarif.GetProperty("runs")[0];
        TestAssertions.Equal("Roslyntic", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString(), "Expected tool name.");

        var rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules").EnumerateArray().ToArray();
        TestAssertions.True(rules.Length >= 2, "Expected AGARCH0001 and AGCOMP0001 rules.");

        foreach (var rule in rules)
        {
            TestAssertions.NotNull(rule.GetProperty("id").GetString(), "Expected rule id.");
            TestAssertions.NotNull(rule.GetProperty("shortDescription").GetProperty("text").GetString(), "Expected short description.");
            TestAssertions.NotNull(rule.GetProperty("fullDescription").GetProperty("text").GetString(), "Expected full description.");
            TestAssertions.NotNull(rule.GetProperty("properties").GetProperty("category").GetString(), "Expected category.");
        }
    }

    [Fact]
    public void Should_emit_driver_rules_sorted_by_rule_id()
    {
        var solutionPath = CliHarness.CreateSampleSolution();
        var (_, stdOut, _) = CliHarness.RunCheck(solutionPath);
        var sarif = SarifReader.Parse(stdOut);
        var run = sarif.GetProperty("runs")[0];
        var rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules").EnumerateArray().ToArray();
        var actual = rules.Select(rule => rule.GetProperty("id").GetString() ?? string.Empty).ToArray();
        var expected = actual.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        TestAssertions.Equal(string.Join(",", expected), string.Join(",", actual), "Expected SARIF driver rules to be sorted by rule id.");
    }

    [Fact]
    public void Should_emit_valid_sarif_with_empty_results_when_no_diagnostics_are_provided()
    {
        var payload = SarifOutputWriter.Write(Array.Empty<DiagnosticFinding>());
        var sarif = SarifReader.Parse(payload);
        var run = sarif.GetProperty("runs")[0];

        TestAssertions.Equal("2.1.0", sarif.GetProperty("version").GetString(), "Expected SARIF version 2.1.0.");
        TestAssertions.Equal(0, run.GetProperty("results").GetArrayLength(), "Expected empty results collection.");
    }
}
