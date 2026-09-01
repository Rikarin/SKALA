// The template declares two holes and the call supplies one value, so `{Total}` renders as the
// literal text `{Total}` and the field a dashboard queries is never populated.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, int count) {
            logger.Information("Loaded {Count} of {Total}", count);
        }
    }
}
