using Roslyntic.Tests.TestSupport;

namespace Roslyntic.Tests.TestSupport;

internal static class SarifReader
{
    public static JsonElement Parse(string stdOut)
    {
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement.Clone();
        return root;
    }

    public static JsonElement Results(JsonElement sarif)
    {
        var run = sarif.GetProperty("runs")[0];
        return run.GetProperty("results");
    }
}
