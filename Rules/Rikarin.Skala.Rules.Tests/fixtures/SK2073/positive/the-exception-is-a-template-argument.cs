// ⚠ Still a finding: the `exception` parameter is empty, so a sink gets a string where it
// expected an exception. The fix attaches the exception and deliberately does not also
// delete the now-redundant hole — removing text from inside a literal needs a value-to-
// source offset map that escape sequences break.
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
        public void Run(Microsoft.Extensions.Logging.ILogger logger) {
            try {
                System.Console.WriteLine("work");
            } catch (System.IO.IOException ex) {
                logger.LogError("it failed: {Error}", ex);
            }
        }
    }
}
