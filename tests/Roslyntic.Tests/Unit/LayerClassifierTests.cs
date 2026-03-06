using Roslyntic.Tests.TestSupport;

namespace Roslyntic.Tests.Unit;

public sealed class LayerClassifierTests
{
    [Fact]
    public void Should_report_AGARCH0001_for_UI_to_Infrastructure_dependency()
    {
        var solutionPath = CliHarness.CreateSampleSolution();
        var (_, stdOut, _) = CliHarness.RunCheck(solutionPath);
        var sarif = SarifReader.Parse(stdOut);
        var results = SarifReader.Results(sarif).EnumerateArray().ToArray();
        TestAssertions.True(
            results.Any(result => result.GetProperty("ruleId").GetString() == "AGARCH0001"),
            "Expected AGARCH0001 result for forbidden UI -> Infrastructure dependency.");
    }

    [Theory]
    [InlineData("Samples.UI", "Ui")]
    [InlineData("samples.ui", "Ui")]
    [InlineData("SAMPLES.APPLICATION", "Application")]
    [InlineData("samples.domain", "Domain")]
    [InlineData("samples.infrastructure", "Infrastructure")]
    public void Should_classify_layer_case_insensitively(string projectName, string expectedLayer)
    {
        var actual = LayerClassifier.FromProjectName(projectName);

        TestAssertions.Equal(expectedLayer, actual?.ToString(), "Expected layer classification to be case insensitive.");
    }

    [Theory]
    [InlineData("Samples.Service")]
    [InlineData("UINotLayer")]
    [InlineData("ApplicationLikeName")]
    public void Should_return_null_for_non_layer_project_names(string projectName)
    {
        var actual = LayerClassifier.FromProjectName(projectName);

        TestAssertions.True(actual is null, "Expected non-layer project names to return null.");
    }
}
