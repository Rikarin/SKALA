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
        public void Run(Report report, int count) {
            report.Information("Loaded {Count} of {Total}", count);
        }
    }
}
