// ⚠ An expected exception, handled, noted in passing. Reporting these is how a rule about
// logging becomes noise about control flow.
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
            } catch (System.IO.FileNotFoundException ex) {
                logger.Information("no file at {Path}; using the default", path);
            }
        }
    }
}
