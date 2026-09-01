using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Logging;

interface ILogger { }

static class LoggerExtensions {
    public static void LogInformation(this ILogger logger, string message, params object[] args) { }
}

sealed class Worker {
    public void Run(Microsoft.Extensions.Logging.ILogger logger, int count) {
        logger.LogInformation($"Loaded {count} items");
    }
}
