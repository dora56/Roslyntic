using Roslyntic.Core;
using Xunit;

namespace Roslyntic.Tests.Core;

public class LayerClassifierTests
{
    // ── known patterns ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Samples.UI")]
    [InlineData("MyApp.UI")]
    [InlineData("Company.Product.UI")]
    public void Classify_ProjectEndingWithUI_ReturnsUiLayer(string projectName)
    {
        // Given / When
        var layer = LayerClassifier.Classify(projectName);

        // Then
        Assert.Equal(Layer.UI, layer);
    }

    [Theory]
    [InlineData("Samples.Application")]
    [InlineData("MyApp.Application")]
    [InlineData("Company.Product.Application")]
    public void Classify_ProjectEndingWithApplication_ReturnsApplicationLayer(string projectName)
    {
        var layer = LayerClassifier.Classify(projectName);

        Assert.Equal(Layer.Application, layer);
    }

    [Theory]
    [InlineData("Samples.Domain")]
    [InlineData("MyApp.Domain")]
    [InlineData("Company.Product.Domain")]
    public void Classify_ProjectEndingWithDomain_ReturnsDomainLayer(string projectName)
    {
        var layer = LayerClassifier.Classify(projectName);

        Assert.Equal(Layer.Domain, layer);
    }

    [Theory]
    [InlineData("Samples.Infrastructure")]
    [InlineData("MyApp.Infrastructure")]
    [InlineData("Company.Product.Infrastructure")]
    public void Classify_ProjectEndingWithInfrastructure_ReturnsInfrastructureLayer(string projectName)
    {
        var layer = LayerClassifier.Classify(projectName);

        Assert.Equal(Layer.Infrastructure, layer);
    }

    // ── unknown / unrecognised patterns ───────────────────────────────────────

    [Theory]
    [InlineData("Samples.Helpers")]
    [InlineData("SharedKernel")]
    [InlineData("")]
    public void Classify_UnrecognisedProjectName_ReturnsNull(string projectName)
    {
        // Given / When
        var layer = LayerClassifier.Classify(projectName);

        // Then: unknown project → no layer
        Assert.Null(layer);
    }

    // ── case sensitivity ───────────────────────────────────────────────────────

    [Fact]
    public void Classify_LowercaseUi_ReturnsNull()
    {
        // Pattern matching is case-sensitive (*.UI not *.ui)
        var layer = LayerClassifier.Classify("Samples.ui");

        Assert.Null(layer);
    }

    // ── partial match must not false-positive ──────────────────────────────────

    [Fact]
    public void Classify_ProjectContainingUIInMiddle_ReturnsNull()
    {
        // "UIHelpers" should not match "*.UI" suffix pattern
        var layer = LayerClassifier.Classify("Samples.UIHelpers");

        Assert.Null(layer);
    }

    [Fact]
    public void Classify_ProjectContainingDomainInMiddle_ReturnsNull()
    {
        // "DomainObjects" does not end with ".Domain"
        var layer = LayerClassifier.Classify("Samples.DomainObjects");

        Assert.Null(layer);
    }
}
