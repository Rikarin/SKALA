// Nothing is reported unless the repair exists: a logging type with no `exception` parameter
// anywhere has no defect this rule can name.
namespace Serilog {
    interface ILogger {
        void Error(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Reader {
        public void Run(Serilog.ILogger logger, string path) {
            try {
                System.Console.WriteLine(path);
            } catch (System.IO.IOException ex) {
                logger.Error("could not read {Path}", path);
            }
        }
    }
}
