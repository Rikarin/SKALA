// ⚠ Measured, not assumed: a probe project built at *default* analysis level — no `AnalysisMode`,
// no `AnalysisLevel` — reports CA2017 on exactly this call, and on `BeginScope` and
// `LoggerMessage.Define` too. ADR-008 hosts CA* rather than rebuilding them, so SK2070 is silent on
// `Microsoft.Extensions.Logging` on purpose.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    static class LoggerExtensions {
        public static void LogInformation(this ILogger logger, string message, params object[] args) { }
    }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class Worker {
        public void Run(Microsoft.Extensions.Logging.ILogger logger, int count) {
            logger.LogInformation("Loaded {Count} of {Total}", count);
        }
    }
}
