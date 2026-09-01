// `catch { }` and `catch (T)` have no exception in scope, so there is nothing to pass and
// no edit that could be written.
namespace Serilog {
    interface ILogger {
        void Error(string messageTemplate, params object[] propertyValues);
        void Error(System.Exception exception, string messageTemplate, params object[] propertyValues);
        void Fatal(string messageTemplate, params object[] propertyValues);
        void Fatal(System.Exception exception, string messageTemplate, params object[] propertyValues);
        void Information(string messageTemplate, params object[] propertyValues);
        void Information(System.Exception exception, string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Reader {
        public void Run(Serilog.ILogger logger, string path) {
            try {
                System.Console.WriteLine(path);
            } catch (System.IO.IOException) {
                logger.Error("could not read {Path}", path);
            }

            try {
                System.Console.WriteLine(path);
            } catch {
                logger.Error("could not read {Path} either", path);
            }
        }
    }
}
