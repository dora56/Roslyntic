using Roslyntic.Core.Logging;

namespace Roslyntic.Cli.Logging;

internal sealed class StderrLogger : IAnalysisLogger
{
    public void LogWarning(string message) => Console.Error.WriteLine($"[WARN]  {message}");
    public void LogError(string message)   => Console.Error.WriteLine($"[ERROR] {message}");
}
