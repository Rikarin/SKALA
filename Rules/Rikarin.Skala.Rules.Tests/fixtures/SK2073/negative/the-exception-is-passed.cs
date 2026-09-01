// The repair, which must not still be reported.
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
            } catch (System.IO.IOException ex) {
                logger.Error(ex, "could not read {Path}", path);
            }
        }
    }
}
