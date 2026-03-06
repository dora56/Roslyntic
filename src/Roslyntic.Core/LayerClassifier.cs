namespace Roslyntic.Core;

public enum Layer
{
    UI,
    Application,
    Domain,
    Infrastructure,
}

/// <summary>
/// Infers the architectural layer from a project name.
/// Pattern matching is case-sensitive and suffix-based.
/// </summary>
public static class LayerClassifier
{
    public static Layer? Classify(string projectName)
    {
        if (projectName.EndsWith(".UI", StringComparison.Ordinal))
            return Layer.UI;
        if (projectName.EndsWith(".Application", StringComparison.Ordinal))
            return Layer.Application;
        if (projectName.EndsWith(".Domain", StringComparison.Ordinal))
            return Layer.Domain;
        if (projectName.EndsWith(".Infrastructure", StringComparison.Ordinal))
            return Layer.Infrastructure;
        return null;
    }
}
