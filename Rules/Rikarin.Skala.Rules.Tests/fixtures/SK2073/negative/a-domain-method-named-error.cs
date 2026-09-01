// Binding is by containing type, never by method name.
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
    sealed class Report {
        public void Error(string messageTemplate, params object[] propertyValues) { }

        public void Error(System.Exception exception, string messageTemplate, params object[] propertyValues) { }
    }

    sealed class Reader {
        public void Run(Report report, string path) {
            try {
                System.Console.WriteLine(path);
            } catch (System.IO.IOException ex) {
                report.Error("could not read {Path}", path);
            }
        }
    }
}
