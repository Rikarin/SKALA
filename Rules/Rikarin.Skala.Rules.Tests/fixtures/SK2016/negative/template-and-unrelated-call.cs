using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Logging;

interface ILogger { }

static class LoggerExtensions {
    public static void LogInformation(this ILogger logger, string message, params object[] args) { }
}

static class OtherLoggerExtensions {
    public static void LogInformation(this object logger, string message) { }
}

sealed class Worker {
    public void Run(Microsoft.Extensions.Logging.ILogger logger, object other, int count) {
        logger.LogInformation("Loaded {Count} items", count);
        other.LogInformation($"Loaded {count} items");
    }
}
