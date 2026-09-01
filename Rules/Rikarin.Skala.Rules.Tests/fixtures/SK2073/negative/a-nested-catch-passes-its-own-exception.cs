// The *nearest* enclosing catch is the one that matters; the inner call uses the inner
// variable and is correct even though an outer catch is also open.
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
            } catch (System.IO.IOException outer) {
                try {
                    logger.Error(outer, "retrying {Path}", path);
                } catch (System.IO.IOException inner) {
                    logger.Error(inner, "gave up on {Path}", path);
                }
            }
        }
    }
}
