using Learnexia.Shared.Kernel.Abstractions;
using NLog;
namespace Learnexia.Shared.Kernel.Logging;
    public class LoggerManager : ILoggerManager
    {
        private static ILogger logger = LogManager.GetCurrentClassLogger();
        public LoggerManager()
        { }
        public void LogDebug(string message) => logger.Debug(message);
        public void LogError(string message) => logger.Error(message);

        public void LogError(Exception ex, string message) => logger.Error(message);
        public void LogInfo(string message) => logger.Info(message);
        public void LogWarn(string message) => logger.Warn(message);
    }

