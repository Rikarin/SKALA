// No CA rule reports this: measured in a probe project at default analysis level and again
// at AnalysisMode=All, nothing fires on a catch that logs without its exception.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    readonly struct EventId {
        public EventId(int id) => Id = id;

        public int Id { get; }
    }

    static class LoggerExtensions {
        public static void LogError(this ILogger logger, string message, params object[] args) { }

        public static void LogError(this ILogger logger, System.Exception exception, string message, params object[] args) { }

        public static void LogError(this ILogger logger, EventId eventId, System.Exception exception, string message, params object[] args) { }

        public static void LogError(this ILogger logger, EventId eventId, string message, params object[] args) { }

        public static void LogInformation(this ILogger logger, string message, params object[] args) { }

        public static void LogInformation(this ILogger logger, System.Exception exception, string message, params object[] args) { }
    }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class Reader {
        public void Run(Microsoft.Extensions.Logging.ILogger logger, string path) {
            try {
                System.Console.WriteLine(path);
            } catch (System.IO.IOException ex) {
                logger.LogError("could not read {Path}", path);
            }
        }
    }
}
