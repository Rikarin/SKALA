// The walk stops at the member declaration, not at the first `try`: a lambda written inside
// a catch really does close over the exception variable, so the call can pass it.
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
        public void Run(Serilog.ILogger logger, System.Action<System.Action> defer) {
            try {
                System.Console.WriteLine("work");
            } catch (System.IO.IOException ex) {
                defer(() => logger.Fatal("gave up after {Attempts}", 3));
            }
        }
    }
}
