namespace Roslyntic.Core.Logging;

public interface IAnalysisLogger
{
    void LogWarning(string message);
    void LogError(string message);
}
