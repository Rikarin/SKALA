// ⚠ The only shape where the template is the first argument written *and* the exception is
// supplied. Without it the exception guard is unreachable — the first-argument guard
// declines every positional `Error(ex, template, …)` before it — and a sabotage that removes
// the exception guard turns nothing red.
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
                logger.Error(messageTemplate: "could not read the file", exception: ex);
            }
        }
    }
}
