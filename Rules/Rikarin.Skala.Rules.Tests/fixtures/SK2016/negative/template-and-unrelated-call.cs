using Microsoft.Extensions.Logging;

namespace Fixtures;

static class OtherLoggerExtensions {
    public static void LogInformation(this object logger, string message) { }
}

sealed class Worker {
    public void Run(Microsoft.Extensions.Logging.ILogger logger, object other, int count) {
        logger.LogInformation("Loaded {Count} items", count);
        other.LogInformation($"Loaded {count} items");
    }
}
