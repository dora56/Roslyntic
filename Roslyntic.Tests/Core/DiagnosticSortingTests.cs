using Roslyntic.Core;
using Xunit;

namespace Roslyntic.Tests.Core;

public class DiagnosticSortingTests
{
    private static Diagnostic MakeDiagnostic(
        string filePath,
        int startLine,
        int startColumn,
        string ruleId = "AGCOMP0001",
        DiagnosticLevel level = DiagnosticLevel.Warning)
        => new(
            RuleId: ruleId,
            Level: level,
            Message: "test message",
            Location: new DiagnosticLocation(filePath, startLine, startColumn, null, null),
            Properties: new Dictionary<string, string>());

    // ── primary sort key: file path (ordinal) ──────────────────────────────────

    [Fact]
    public void Sort_DifferentFilePaths_SortedByOrdinalPath()
    {
        // Given: z.cs comes before a.cs in reverse lexicographic order
        var diagnostics = new[]
        {
            MakeDiagnostic("z.cs", 1, 1),
            MakeDiagnostic("a.cs", 1, 1),
            MakeDiagnostic("m.cs", 1, 1),
        };

        // When
        var sorted = DiagnosticSorter.Sort(diagnostics).ToList();

        // Then: ordinal ascending
        Assert.Equal("a.cs", sorted[0].Location.FilePath);
        Assert.Equal("m.cs", sorted[1].Location.FilePath);
        Assert.Equal("z.cs", sorted[2].Location.FilePath);
    }

    // ── secondary sort key: start line ────────────────────────────────────────

    [Fact]
    public void Sort_SameFile_SortedByStartLine()
    {
        // Given
        var diagnostics = new[]
        {
            MakeDiagnostic("a.cs", 30, 1),
            MakeDiagnostic("a.cs",  5, 1),
            MakeDiagnostic("a.cs", 15, 1),
        };

        // When
        var sorted = DiagnosticSorter.Sort(diagnostics).ToList();

        // Then
        Assert.Equal(5,  sorted[0].Location.StartLine);
        Assert.Equal(15, sorted[1].Location.StartLine);
        Assert.Equal(30, sorted[2].Location.StartLine);
    }

    // ── tertiary sort key: start column ───────────────────────────────────────

    [Fact]
    public void Sort_SameFileSameLine_SortedByStartColumn()
    {
        // Given
        var diagnostics = new[]
        {
            MakeDiagnostic("a.cs", 10, 20),
            MakeDiagnostic("a.cs", 10,  5),
            MakeDiagnostic("a.cs", 10, 12),
        };

        // When
        var sorted = DiagnosticSorter.Sort(diagnostics).ToList();

        // Then
        Assert.Equal(5,  sorted[0].Location.StartColumn);
        Assert.Equal(12, sorted[1].Location.StartColumn);
        Assert.Equal(20, sorted[2].Location.StartColumn);
    }

    // ── quaternary sort key: ruleId ────────────────────────────────────────────

    [Fact]
    public void Sort_SameLocation_SortedByRuleId()
    {
        // Given: identical location, different ruleIds
        var diagnostics = new[]
        {
            MakeDiagnostic("a.cs", 1, 1, "AGCOMP0001"),
            MakeDiagnostic("a.cs", 1, 1, "AGARCH0001"),
        };

        // When
        var sorted = DiagnosticSorter.Sort(diagnostics).ToList();

        // Then: ordinal string order
        Assert.Equal("AGARCH0001", sorted[0].RuleId);
        Assert.Equal("AGCOMP0001", sorted[1].RuleId);
    }

    // ── empty / single ────────────────────────────────────────────────────────

    [Fact]
    public void Sort_EmptyList_ReturnsEmpty()
    {
        var sorted = DiagnosticSorter.Sort(Array.Empty<Diagnostic>()).ToList();

        Assert.Empty(sorted);
    }

    [Fact]
    public void Sort_SingleElement_ReturnsSingleElement()
    {
        var d = MakeDiagnostic("a.cs", 1, 1);
        var sorted = DiagnosticSorter.Sort(new[] { d }).ToList();

        Assert.Single(sorted);
        Assert.Same(d, sorted[0]);
    }

    // ── idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public void Sort_AlreadySorted_DoesNotChangeOrder()
    {
        // Given: already in correct order
        var diagnostics = new[]
        {
            MakeDiagnostic("a.cs", 1,  1, "AGARCH0001"),
            MakeDiagnostic("a.cs", 1,  5, "AGCOMP0001"),
            MakeDiagnostic("b.cs", 1,  1, "AGARCH0001"),
        };

        // When
        var sorted = DiagnosticSorter.Sort(diagnostics).ToList();

        // Then: same order preserved
        Assert.Equal("a.cs", sorted[0].Location.FilePath);
        Assert.Equal("AGARCH0001", sorted[0].RuleId);
        Assert.Equal("a.cs", sorted[1].Location.FilePath);
        Assert.Equal("AGCOMP0001", sorted[1].RuleId);
        Assert.Equal("b.cs", sorted[2].Location.FilePath);
    }
}
