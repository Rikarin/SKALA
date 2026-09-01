// One hole, two values. The second is attached to the event under a fabricated positional key,
// so the data is present, unfindable and paid for.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, int count, int total) {
            logger.Information("Loaded {Count}", count, total);
        }
    }
}
