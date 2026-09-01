// `{{` and `}}` are escapes. A parser that misses that reports a template printing a literal brace
// as having a hole nobody supplied an argument for.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, int count) {
            logger.Information("{{Count}} is a literal brace, {Count} is a hole", count);
            logger.Information("}} alone renders verbatim, {Count} is the only hole", count);
        }
    }
}
