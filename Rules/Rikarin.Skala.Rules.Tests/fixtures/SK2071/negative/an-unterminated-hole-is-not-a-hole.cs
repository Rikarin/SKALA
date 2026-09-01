// A `{` with no closing brace is text the logger renders verbatim. Nothing after it parses either,
// so the trailing `{Path}` is not a second occurrence of anything.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, string path) {
            logger.Information("{Path unterminated and then {Path", path);
        }
    }
}
