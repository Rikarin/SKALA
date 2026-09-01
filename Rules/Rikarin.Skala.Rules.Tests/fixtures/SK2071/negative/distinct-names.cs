namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, string from, string to) {
            logger.Information("Moved {From} to {To}", from, to);

            // The same name in two *calls* is two events, each with one value under the key.
            logger.Information("Moved {From}", from);
            logger.Information("Arrived at {From}", to);
        }
    }
}
