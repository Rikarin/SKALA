// ⚠ Declined outright rather than reported. Microsoft.Extensions.Logging orders this
// overload (EventId, Exception, string), so an exception prepended in front of the event id
// does not bind — and a finding whose fix breaks the build is worse than no finding.
namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class Reader {
        public void Run(Microsoft.Extensions.Logging.ILogger logger, string path) {
            try {
                System.Console.WriteLine(path);
            } catch (System.IO.IOException ex) {
                logger.LogError(new EventId(7), "could not read {Path}", path);
            }
        }
    }
}
