using System.Text.Json;
using Roslyntic.Core;
using Roslyntic.Sarif;
using Xunit;

namespace Roslyntic.Tests.Sarif;

/// <summary>
/// Tests for SARIF 2.1.0 output produced by <see cref="SarifSerializer"/>.
/// Covers structural correctness, required fields, determinism, and no-side-effects.
/// </summary>
public class SarifSerializerTests
{
    private const string ToolVersion = "1.0.0";

    private static readonly RuleMetadata[] NoMetadata = [];

    private static RuleMetadata MakeMetadata(
        string id = "AGCOMP0001",
        string title = "Test Rule",
        string description = "Test rule description.",
        string category = "Test",
        DiagnosticLevel defaultLevel = DiagnosticLevel.Warning)
        => new(id, title, description, category, defaultLevel, HelpUri: null);

    private static Diagnostic MakeDiagnostic(
        string ruleId = "AGCOMP0001",
        DiagnosticLevel level = DiagnosticLevel.Warning,
        string message = "Complexity exceeded.",
        string filePath = "/src/Foo.cs",
        int startLine = 10,
        int startColumn = 5,
        IReadOnlyDictionary<string, string>? properties = null)
        => new(
            RuleId: ruleId,
            Level: level,
            Message: message,
            Location: new DiagnosticLocation(filePath, startLine, startColumn, null, null),
            Properties: properties ?? new Dictionary<string, string>());

    // ── structural shape ──────────────────────────────────────────────────────

    [Fact]
    public void Serialize_EmptyDiagnostics_ProducesValidSarifEnvelope()
    {
        // Given: no findings
        var json = SarifSerializer.Serialize(Array.Empty<Diagnostic>(), NoMetadata, ToolVersion);

        // When: parsed as JSON
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Then: required SARIF envelope fields are present
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        Assert.True(root.TryGetProperty("$schema", out _));
        var runs = root.GetProperty("runs");
        Assert.Equal(JsonValueKind.Array, runs.ValueKind);
        Assert.Equal(1, runs.GetArrayLength());
    }

    [Fact]
    public void Serialize_EmptyDiagnostics_HasEmptyResultsArray()
    {
        // Given / When
        var json = SarifSerializer.Serialize(Array.Empty<Diagnostic>(), NoMetadata, ToolVersion);

        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("results");

        // Then
        Assert.Equal(JsonValueKind.Array, results.ValueKind);
        Assert.Equal(0, results.GetArrayLength());
    }

    [Fact]
    public void Serialize_EmptyDiagnostics_ToolNameIsRoslyntic()
    {
        // Given / When
        var json = SarifSerializer.Serialize(Array.Empty<Diagnostic>(), NoMetadata, ToolVersion);

        using var doc = JsonDocument.Parse(json);
        var driverName = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("tool")
            .GetProperty("driver")
            .GetProperty("name")
            .GetString();

        // Then
        Assert.Equal("Roslyntic", driverName);
    }

    [Fact]
    public void Serialize_EmptyDiagnostics_ToolVersionMatches()
    {
        // Given / When
        var json = SarifSerializer.Serialize(Array.Empty<Diagnostic>(), NoMetadata, ToolVersion);

        using var doc = JsonDocument.Parse(json);
        var version = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("tool")
            .GetProperty("driver")
            .GetProperty("version")
            .GetString();

        // Then
        Assert.Equal(ToolVersion, version);
    }

    // ── result mapping ────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_SingleDiagnostic_MapsRuleId()
    {
        // Given
        var d = MakeDiagnostic(ruleId: "AGCOMP0001");

        // When
        var json = SarifSerializer.Serialize(new[] { d }, NoMetadata, ToolVersion);

        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        // Then
        Assert.Equal("AGCOMP0001", result.GetProperty("ruleId").GetString());
    }

    [Theory]
    [InlineData(DiagnosticLevel.Error,   "error")]
    [InlineData(DiagnosticLevel.Warning, "warning")]
    [InlineData(DiagnosticLevel.Note,    "note")]
    public void Serialize_DiagnosticLevel_MapsToSarifLevel(DiagnosticLevel level, string expected)
    {
        // Given
        var d = MakeDiagnostic(level: level);

        // When
        var json = SarifSerializer.Serialize(new[] { d }, NoMetadata, ToolVersion);

        using var doc = JsonDocument.Parse(json);
        var sarifLevel = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("results")[0]
            .GetProperty("level")
            .GetString();

        // Then
        Assert.Equal(expected, sarifLevel);
    }

    [Fact]
    public void Serialize_SingleDiagnostic_MapsPhysicalLocation()
    {
        // Given
        var d = MakeDiagnostic(filePath: "/src/Bar.cs", startLine: 42, startColumn: 8);

        // When
        var json = SarifSerializer.Serialize(new[] { d }, NoMetadata, ToolVersion);

        using var doc = JsonDocument.Parse(json);
        var physicalLocation = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("results")[0]
            .GetProperty("locations")[0]
            .GetProperty("physicalLocation");

        var uri     = physicalLocation.GetProperty("artifactLocation").GetProperty("uri").GetString();
        var line    = physicalLocation.GetProperty("region").GetProperty("startLine").GetInt32();
        var column  = physicalLocation.GetProperty("region").GetProperty("startColumn").GetInt32();

        // Then
        Assert.Equal(new Uri(Path.GetFullPath("/src/Bar.cs")).AbsoluteUri, uri);
        Assert.Equal(42, line);
        Assert.Equal(8,  column);
    }

    [Fact]
    public void Serialize_SingleDiagnostic_MapsMessageText()
    {
        // Given
        var d = MakeDiagnostic(message: "Method 'Foo' has complexity 20 (threshold 15).");

        // When
        var json = SarifSerializer.Serialize(new[] { d }, NoMetadata, ToolVersion);

        using var doc = JsonDocument.Parse(json);
        var messageText = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("results")[0]
            .GetProperty("message")
            .GetProperty("text")
            .GetString();

        // Then
        Assert.Equal("Method 'Foo' has complexity 20 (threshold 15).", messageText);
    }

    // ── determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_SameInput_ProducesBitwiseIdenticalOutput()
    {
        // Given
        var diagnostics = new[]
        {
            MakeDiagnostic("AGARCH0001", DiagnosticLevel.Warning, "Violation.", "/src/A.cs", 1, 1),
            MakeDiagnostic("AGCOMP0001", DiagnosticLevel.Error,   "Complexity.", "/src/B.cs", 5, 3),
        };

        // When: serialize twice
        var metadata = new[] { MakeMetadata("AGARCH0001"), MakeMetadata("AGCOMP0001") };
        var first  = SarifSerializer.Serialize(diagnostics, metadata, ToolVersion);
        var second = SarifSerializer.Serialize(diagnostics, metadata, ToolVersion);

        // Then: byte-identical
        Assert.Equal(first, second);
    }

    [Fact]
    public void Serialize_DoesNotContainTimestamps()
    {
        // SARIF timestamps would break determinism
        var json = SarifSerializer.Serialize(Array.Empty<Diagnostic>(), NoMetadata, ToolVersion);

        // heuristic: if a field named "startTimeUtc" or "endTimeUtc" appears, it is a violation
        using var doc = JsonDocument.Parse(json);
        var raw = json;

        Assert.DoesNotContain("startTimeUtc", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endTimeUtc",   raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_DoesNotContainGuids()
    {
        // GUIDs (e.g. "automationId", "correlationGuid") would break determinism
        var json = SarifSerializer.Serialize(Array.Empty<Diagnostic>(), NoMetadata, ToolVersion);

        Assert.DoesNotContain("Guid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correlationGuid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("automationId", json, StringComparison.OrdinalIgnoreCase);
    }

    // ── rules section ─────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_DiagnosticsWithDistinctRules_RulesSectionContainsEachRuleOnce()
    {
        // Given: two diagnostics with different ruleIds
        var diagnostics = new[]
        {
            MakeDiagnostic("AGARCH0001"),
            MakeDiagnostic("AGCOMP0001"),
        };

        // When
        var metadata = new[]
        {
            MakeMetadata("AGARCH0001", title: "Layer violation",   category: "Architecture"),
            MakeMetadata("AGCOMP0001", title: "Complexity rule",   category: "Complexity"),
        };
        var json = SarifSerializer.Serialize(diagnostics, metadata, ToolVersion);

        using var doc = JsonDocument.Parse(json);
        var rules = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("tool")
            .GetProperty("driver")
            .GetProperty("rules");

        Assert.Equal(JsonValueKind.Array, rules.ValueKind);
        var ruleIds = rules.EnumerateArray()
            .Select(r => r.GetProperty("id").GetString())
            .ToList();

        Assert.Contains("AGARCH0001", ruleIds);
        Assert.Contains("AGCOMP0001", ruleIds);
        // No duplicates
        Assert.Equal(ruleIds.Distinct().Count(), ruleIds.Count);
    }

    [Fact]
    public void Serialize_WithMetadata_PopulatesShortDescriptionAndFullDescriptionAndCategory()
    {
        // Given
        var d = MakeDiagnostic(ruleId: "AGCOMP0001");
        var meta = MakeMetadata(
            id: "AGCOMP0001",
            title: "Cyclomatic complexity threshold exceeded",
            description: "Reports methods whose cyclomatic complexity exceeds the configured threshold.",
            category: "Complexity");

        // When
        var json = SarifSerializer.Serialize(new[] { d }, new[] { meta }, ToolVersion);

        using var doc = JsonDocument.Parse(json);
        var rule = doc.RootElement
            .GetProperty("runs")[0]
            .GetProperty("tool")
            .GetProperty("driver")
            .GetProperty("rules")[0];

        // Then: required SARIF rule fields are present
        Assert.Equal("Cyclomatic complexity threshold exceeded",
            rule.GetProperty("shortDescription").GetProperty("text").GetString());
        Assert.Equal("Reports methods whose cyclomatic complexity exceeds the configured threshold.",
            rule.GetProperty("fullDescription").GetProperty("text").GetString());
        Assert.Equal("Complexity",
            rule.GetProperty("properties").GetProperty("category").GetString());
    }
}
