using Roslyntic.Tests.TestSupport;

namespace Roslyntic.Tests.Unit;

public sealed class DiagnosticSorterTests
{
    [Fact]
    public void Should_emit_results_sorted_by_path_line_column_ruleId()
    {
        var solutionPath = CliHarness.CreateSampleSolution();
        var (_, stdOut, _) = CliHarness.RunCheck(solutionPath);
        var sarif = SarifReader.Parse(stdOut);
        var results = SarifReader.Results(sarif).EnumerateArray().ToArray();
        var ordered = results
            .Select(ToKey)
            .OrderBy(key => key.Path, StringComparer.Ordinal)
            .ThenBy(key => key.Line)
            .ThenBy(key => key.Column)
            .ThenBy(key => key.RuleId, StringComparer.Ordinal)
            .ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            TestAssertions.Equal(
                $"{ordered[i].Path}:{ordered[i].Line}:{ordered[i].Column}:{ordered[i].RuleId}",
                $"{ToKey(results[i]).Path}:{ToKey(results[i]).Line}:{ToKey(results[i]).Column}:{ToKey(results[i]).RuleId}",
                $"Expected deterministic ordering at index {i}.");
        }
    }

    [Fact]
    public void Should_return_identical_order_for_different_input_permutations()
    {
        var first = new[]
        {
            CreateFinding("b.cs", 10, 2, "AGCOMP0001"),
            CreateFinding("a.cs", 1, 1, "AGARCH0001"),
            CreateFinding("a.cs", 1, 1, "AGCOMP0001")
        };
        var second = new[]
        {
            first[2],
            first[0],
            first[1]
        };

        var sortedFirst = DiagnosticSorter.Sort(first);
        var sortedSecond = DiagnosticSorter.Sort(second);

        for (var i = 0; i < sortedFirst.Count; i++)
        {
            TestAssertions.Equal(ToKey(sortedFirst[i]), ToKey(sortedSecond[i]), $"Expected identical sorted order at index {i}.");
        }
    }

    [Fact]
    public void Should_sort_by_normalized_path_then_line_column_then_rule_id()
    {
        var diagnostics = new[]
        {
            CreateFinding("z.cs", 1, 1, "AGCOMP0001"),
            CreateFinding("a.cs", 2, 1, "AGCOMP0001"),
            CreateFinding("a.cs", 1, 2, "AGCOMP0001"),
            CreateFinding("a.cs", 1, 2, "AGARCH0001")
        };

        var sorted = DiagnosticSorter.Sort(diagnostics);
        var actual = sorted.Select(ToKey).ToArray();
        var expected = actual
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Line)
            .ThenBy(item => item.Column)
            .ThenBy(item => item.RuleId, StringComparer.Ordinal)
            .ToArray();

        for (var i = 0; i < actual.Length; i++)
        {
            TestAssertions.Equal(expected[i], actual[i], $"Expected stable sorted key at index {i}.");
        }
    }

    private static (string Path, int Line, int Column, string RuleId) ToKey(JsonElement result)
    {
        var location = result.GetProperty("locations")[0].GetProperty("physicalLocation");
        var path = location.GetProperty("artifactLocation").GetProperty("uri").GetString() ?? string.Empty;
        var region = location.GetProperty("region");
        var line = region.GetProperty("startLine").GetInt32();
        var column = region.GetProperty("startColumn").GetInt32();
        var ruleId = result.GetProperty("ruleId").GetString() ?? string.Empty;
        return (path, line, column, ruleId);
    }

    private static (string Path, int Line, int Column, string RuleId) ToKey(DiagnosticFinding finding)
    {
        return (PathNormalizer.Normalize(finding.Location.Path), finding.Location.StartLine, finding.Location.StartColumn, finding.RuleId);
    }

    private static DiagnosticFinding CreateFinding(string path, int line, int column, string ruleId)
    {
        return new DiagnosticFinding(
            ruleId,
            "warning",
            "message",
            new SourceLocation(path, line, column, line, column),
            new Dictionary<string, string>(StringComparer.Ordinal));
    }
}
