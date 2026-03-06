internal static class RuleCatalog
{
    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        new RuleDefinition(
            CliContract.ArchRuleId,
            "Layer violation",
            "Detects forbidden layer dependencies.",
            "architecture",
            "https://example.invalid/rules/AGARCH0001"),
        new RuleDefinition(
            CliContract.ComplexityRuleId,
            "Cyclomatic complexity threshold exceeded",
            "Reports methods with complexity greater than the configured threshold.",
            "complexity",
            "https://example.invalid/rules/AGCOMP0001")
    ];
}

internal sealed record RuleDefinition(string Id, string ShortDescription, string FullDescription, string Category, string HelpUri);
