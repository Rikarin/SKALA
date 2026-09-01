namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
        void Error(System.Exception exception, string messageTemplate, params object[] propertyValues);
    }

    static class Log {
        public static void Information(string messageTemplate, params object[] propertyValues) { }
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, System.Exception error, int count, int total) {
            logger.Information("Loaded {Count} of {Total}", count, total);
            logger.Information("Nothing to say");

            // ⚠ The exception precedes the template and is not a value. Counting arguments by type
            // rather than by ordinal reports this as one value too many.
            logger.Error(error, "Failed after {Count}", count);
            Serilog.Log.Information("Static entry point, {Count}", count);
        }
    }
}
