// Binding is by containing type and parameter name, never by method name.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Report {
        public void Information(string messageTemplate, params object[] propertyValues) { }
    }

    sealed class Worker {
        public void Run(Report report, string from, string to) {
            report.Information("Moved {Path} to {Path}", from, to);
        }
    }
}
