namespace Learnexia.Shared.Kernel.Abstractions;

public interface ILoggerManager
{
    void LogInfo(string message);
    void LogWarn(string message);
    void LogDebug(string message);
    void LogError(Exception? ex, string message);
}
