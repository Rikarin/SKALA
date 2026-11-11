using Microsoft.Extensions.Logging;

namespace Fixtures;

sealed class Worker {
    public void Run(Microsoft.Extensions.Logging.ILogger logger, int count) {
        logger.LogInformation($"Loaded {count} items");
    }
}
