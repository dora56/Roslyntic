using System.Text.Json;
using System.Text.Json.Serialization;

internal static class JsonOutputWriter
{
    public static string Write(IReadOnlyList<DiagnosticFinding> diagnostics)
    {
        var payload = new JsonOutput(
            RuleCatalog.All
                .OrderBy(rule => rule.Id, StringComparer.Ordinal)
                .Select(rule => new JsonRule(rule.Id, rule.ShortDescription, rule.FullDescription, rule.Category, rule.HelpUri))
                .ToArray(),
            diagnostics.Select(diagnostic => new JsonDiagnostic(
                diagnostic.RuleId,
                diagnostic.Level,
                diagnostic.Message,
                new JsonLocation(
                    PathNormalizer.Normalize(diagnostic.Location.Path),
                    diagnostic.Location.StartLine,
                    diagnostic.Location.StartColumn,
                    diagnostic.Location.EndLine,
                    diagnostic.Location.EndColumn),
                diagnostic.Properties))
            .ToArray());

        return JsonSerializer.Serialize(payload);
    }
}

internal sealed record JsonOutput(
    [property: JsonPropertyName("rules")] IReadOnlyList<JsonRule> Rules,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<JsonDiagnostic> Diagnostics);

internal sealed record JsonRule(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("shortDescription")] string ShortDescription,
    [property: JsonPropertyName("fullDescription")] string FullDescription,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("helpUri")] string HelpUri);

internal sealed record JsonDiagnostic(
    [property: JsonPropertyName("ruleId")] string RuleId,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("location")] JsonLocation Location,
    [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, string> Properties);

internal sealed record JsonLocation(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("startLine")] int StartLine,
    [property: JsonPropertyName("startColumn")] int StartColumn,
    [property: JsonPropertyName("endLine")] int EndLine,
    [property: JsonPropertyName("endColumn")] int EndColumn);
