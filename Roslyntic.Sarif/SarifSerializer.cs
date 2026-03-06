using System.Text.Json;
using System.Text.Json.Serialization;
using Roslyntic.Core;

namespace Roslyntic.Sarif;

/// <summary>
/// Serializes diagnostics to SARIF 2.1.0 JSON.
/// Output is deterministic (no timestamps, no GUIDs).
/// </summary>
public static class SarifSerializer
{
    private const string SarifVersion = "2.1.0";
    private const string SchemaUri    = "https://schemastore.azurewebsites.net/schemas/json/sarif-2.1.0.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize(
        IEnumerable<Diagnostic> diagnostics,
        IEnumerable<RuleMetadata> ruleMetadata,
        string toolVersion)
    {
        var diagList = diagnostics.ToList();
        var metadataLookup = ruleMetadata.ToDictionary(m => m.Id, StringComparer.Ordinal);

        var ruleIds = diagList
            .Select(d => d.RuleId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var rules = ruleIds.Select(id =>
        {
            metadataLookup.TryGetValue(id, out var meta);
            return new SarifRule
            {
                Id = id,
                ShortDescription = meta is not null ? new SarifMessage { Text = meta.Title } : null,
                FullDescription = meta is not null ? new SarifMessage { Text = meta.Description } : null,
                HelpUri = meta?.HelpUri,
                Properties = meta is not null ? new SarifRuleProperties { Category = meta.Category } : null,
            };
        }).ToList();

        var results = diagList.Select(d => new SarifResult
        {
            RuleId  = d.RuleId,
            Level   = MapLevel(d.Level),
            Message = new SarifMessage { Text = d.Message },
            Locations =
            [
                new SarifLocation
                {
                    PhysicalLocation = new SarifPhysicalLocation
                    {
                        ArtifactLocation = new SarifArtifactLocation { Uri = new Uri(Path.GetFullPath(d.Location.FilePath)).AbsoluteUri },
                        Region = new SarifRegion
                        {
                            StartLine   = d.Location.StartLine,
                            StartColumn = d.Location.StartColumn,
                            EndLine     = d.Location.EndLine,
                            EndColumn   = d.Location.EndColumn,
                        },
                    },
                },
            ],
        }).ToList();

        var document = new SarifDocument
        {
            Version = SarifVersion,
            Schema  = SchemaUri,
            Runs =
            [
                new SarifRun
                {
                    Tool = new SarifTool
                    {
                        Driver = new SarifDriver
                        {
                            Name    = "Roslyntic",
                            Version = toolVersion,
                            Rules   = rules,
                        },
                    },
                    Results = results,
                },
            ],
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static string MapLevel(DiagnosticLevel level) => level switch
    {
        DiagnosticLevel.Error   => "error",
        DiagnosticLevel.Warning => "warning",
        DiagnosticLevel.Note    => "note",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unexpected diagnostic level."),
    };

    // ── SARIF POCO models ─────────────────────────────────────────────────────

    private sealed class SarifDocument
    {
        [JsonPropertyName("version")]
        public required string Version { get; init; }

        [JsonPropertyName("$schema")]
        public required string Schema { get; init; }

        [JsonPropertyName("runs")]
        public required List<SarifRun> Runs { get; init; }
    }

    private sealed class SarifRun
    {
        [JsonPropertyName("tool")]
        public required SarifTool Tool { get; init; }

        [JsonPropertyName("results")]
        public required List<SarifResult> Results { get; init; }
    }

    private sealed class SarifTool
    {
        [JsonPropertyName("driver")]
        public required SarifDriver Driver { get; init; }
    }

    private sealed class SarifDriver
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("version")]
        public required string Version { get; init; }

        [JsonPropertyName("rules")]
        public required List<SarifRule> Rules { get; init; }
    }

    private sealed class SarifRule
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("shortDescription")]
        public SarifMessage? ShortDescription { get; init; }

        [JsonPropertyName("fullDescription")]
        public SarifMessage? FullDescription { get; init; }

        [JsonPropertyName("helpUri")]
        public string? HelpUri { get; init; }

        [JsonPropertyName("properties")]
        public SarifRuleProperties? Properties { get; init; }
    }

    private sealed class SarifRuleProperties
    {
        [JsonPropertyName("category")]
        public required string Category { get; init; }
    }

    private sealed class SarifResult
    {
        [JsonPropertyName("ruleId")]
        public required string RuleId { get; init; }

        [JsonPropertyName("level")]
        public required string Level { get; init; }

        [JsonPropertyName("message")]
        public required SarifMessage Message { get; init; }

        [JsonPropertyName("locations")]
        public required List<SarifLocation> Locations { get; init; }
    }

    private sealed class SarifMessage
    {
        [JsonPropertyName("text")]
        public required string Text { get; init; }
    }

    private sealed class SarifLocation
    {
        [JsonPropertyName("physicalLocation")]
        public required SarifPhysicalLocation PhysicalLocation { get; init; }
    }

    private sealed class SarifPhysicalLocation
    {
        [JsonPropertyName("artifactLocation")]
        public required SarifArtifactLocation ArtifactLocation { get; init; }

        [JsonPropertyName("region")]
        public required SarifRegion Region { get; init; }
    }

    private sealed class SarifArtifactLocation
    {
        [JsonPropertyName("uri")]
        public required string Uri { get; init; }
    }

    private sealed class SarifRegion
    {
        [JsonPropertyName("startLine")]
        public int StartLine { get; init; }

        [JsonPropertyName("startColumn")]
        public int StartColumn { get; init; }

        [JsonPropertyName("endLine")]
        public int? EndLine { get; init; }

        [JsonPropertyName("endColumn")]
        public int? EndColumn { get; init; }
    }
}
