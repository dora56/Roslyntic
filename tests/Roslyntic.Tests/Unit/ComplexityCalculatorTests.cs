using Roslyntic.Tests.TestSupport;

namespace Roslyntic.Tests.Unit;

public sealed class ComplexityCalculatorTests
{
    [Fact]
    public void Should_report_AGCOMP0001_when_complexity_exceeds_threshold()
    {
        var solutionPath = CliHarness.CreateSampleSolution();
        var (_, stdOut, _) = CliHarness.RunCheck(solutionPath);
        var sarif = SarifReader.Parse(stdOut);
        var results = SarifReader.Results(sarif).EnumerateArray().ToArray();
        TestAssertions.True(
            results.Any(result => result.GetProperty("ruleId").GetString() == "AGCOMP0001"),
            "Expected AGCOMP0001 result for complexity > 15.");
    }

    [Fact]
    public void Should_keep_complexity_at_or_above_one_for_generated_method_bodies()
    {
        var random = new Random(42);
        var tokens = new[]
        {
            "if (a > 0) { }",
            "for (var i = 0; i < 1; i++) { }",
            "while (a > 0) { }",
            "switch (a) { case 0: break; }",
            "try { } catch (Exception) { }",
            "var x = a > 1 && a < 10;",
            "var y = a > 3 || a < -3;",
            "var z = a > 1 ? 1 : 0;"
        };

        for (var i = 0; i < 100; i++)
        {
            var body = GenerateBody(random, tokens);

            var complexity = ComplexityCalculator.Calculate(body);

            TestAssertions.True(complexity >= 1, "Expected complexity to remain >= 1.");
        }
    }

    [Fact]
    public void Should_not_decrease_complexity_when_branching_tokens_are_added()
    {
        var snippets = new[]
        {
            "if (a > 0) { }",
            "case 1: break;",
            "for (var i = 0; i < 1; i++) { }",
            "foreach (var x in xs) { }",
            "while (a > 0) { }",
            "do { a--; } while (a > 0);",
            "catch (Exception) { }",
            "var b = a > 0 && a < 10;",
            "var c = a > 0 || a < -10;",
            "var d = a > 0 ? 1 : 0;"
        };

        var body = string.Empty;
        var previous = ComplexityCalculator.Calculate(body);
        foreach (var snippet in snippets)
        {
            body += Environment.NewLine + snippet;
            var current = ComplexityCalculator.Calculate(body);

            TestAssertions.True(current >= previous, "Expected complexity to be monotonic when tokens are added.");
            previous = current;
        }
    }

    private static string GenerateBody(Random random, IReadOnlyList<string> tokens)
    {
        var count = random.Next(0, 8);
        var parts = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            parts.Add(tokens[random.Next(tokens.Count)]);
        }

        return string.Join(Environment.NewLine, parts);
    }
}
