internal enum Layer
{
    Ui,
    Application,
    Domain,
    Infrastructure
}

internal static class LayerClassifier
{
    public static Layer? FromProjectName(string projectName)
    {
        if (projectName.Contains(".UI", StringComparison.OrdinalIgnoreCase) || projectName.EndsWith("UI", StringComparison.OrdinalIgnoreCase))
        {
            return Layer.Ui;
        }

        if (projectName.Contains(".Application", StringComparison.OrdinalIgnoreCase) || projectName.EndsWith("Application", StringComparison.OrdinalIgnoreCase))
        {
            return Layer.Application;
        }

        if (projectName.Contains(".Domain", StringComparison.OrdinalIgnoreCase) || projectName.EndsWith("Domain", StringComparison.OrdinalIgnoreCase))
        {
            return Layer.Domain;
        }

        if (projectName.Contains(".Infrastructure", StringComparison.OrdinalIgnoreCase) || projectName.EndsWith("Infrastructure", StringComparison.OrdinalIgnoreCase))
        {
            return Layer.Infrastructure;
        }

        return null;
    }
}
