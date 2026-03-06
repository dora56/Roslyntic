using System.Text.Json;
using System.Text.Json.Serialization;

internal static class SarifOutputWriter
{
    private const string ToolName = "Roslyntic";
    private const string ToolVersion = "0.1.0";

    public static string Write(IReadOnlyList<DiagnosticFinding> diagnostics)
    {
        var run = new SarifRun(
            new SarifTool(
                new SarifDriver(
                    ToolName,
                    ToolVersion,
                    "https://github.com/dora56/Roslyntic",
                    RuleCatalog.All
                        .OrderBy(rule => rule.Id, StringComparer.Ordinal)
                        .Select(rule => new SarifRule(
                            rule.Id,
                            new SarifText(rule.ShortDescription),
                            new SarifText(rule.FullDescription),
                            rule.HelpUri,
                            new SarifRuleProperties(rule.Category)))
                        .ToArray())),
            diagnostics.Select(diagnostic => new SarifResult(
                diagnostic.RuleId,
                diagnostic.Level,
                new SarifText(diagnostic.Message),
                [new SarifLocation(new SarifPhysicalLocation(
                    new SarifArtifactLocation(PathNormalizer.Normalize(diagnostic.Location.Path)),
                    new SarifRegion(
                        diagnostic.Location.StartLine,
                        diagnostic.Location.StartColumn,
                        diagnostic.Location.EndLine,
                        diagnostic.Location.EndColumn)))],
                diagnostic.Properties))
            .ToArray());

        var payload = new SarifLog("2.1.0", [run]);
        return JsonSerializer.Serialize(payload);
    }
}

internal sealed record SarifLog(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("runs")] IReadOnlyList<SarifRun> Runs);

internal sealed record SarifRun(
    [property: JsonPropertyName("tool")] SarifTool Tool,
    [property: JsonPropertyName("results")] IReadOnlyList<SarifResult> Results);

internal sealed record SarifTool(
    [property: JsonPropertyName("driver")] SarifDriver Driver);

internal sealed record SarifDriver(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("informationUri")] string InformationUri,
    [property: JsonPropertyName("rules")] IReadOnlyList<SarifRule> Rules);

internal sealed record SarifRule(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("shortDescription")] SarifText ShortDescription,
    [property: JsonPropertyName("fullDescription")] SarifText FullDescription,
    [property: JsonPropertyName("helpUri")] string HelpUri,
    [property: JsonPropertyName("properties")] SarifRuleProperties Properties);

internal sealed record SarifRuleProperties(
    [property: JsonPropertyName("category")] string Category);

internal sealed record SarifResult(
    [property: JsonPropertyName("ruleId")] string RuleId,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("message")] SarifText Message,
    [property: JsonPropertyName("locations")] IReadOnlyList<SarifLocation> Locations,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, string> Properties);

internal sealed record SarifText(
    [property: JsonPropertyName("text")] string Text);

internal sealed record SarifLocation(
    [property: JsonPropertyName("physicalLocation")] SarifPhysicalLocation PhysicalLocation);

internal sealed record SarifPhysicalLocation(
    [property: JsonPropertyName("artifactLocation")] SarifArtifactLocation ArtifactLocation,
    [property: JsonPropertyName("region")] SarifRegion Region);

internal sealed record SarifArtifactLocation(
    [property: JsonPropertyName("uri")] string Uri);

internal sealed record SarifRegion(
    [property: JsonPropertyName("startLine")] int StartLine,
    [property: JsonPropertyName("startColumn")] int StartColumn,
    [property: JsonPropertyName("endLine")] int EndLine,
    [property: JsonPropertyName("endColumn")] int EndColumn);
