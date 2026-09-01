// Arity is correct — two holes, two values — so SK2070 is silent and so is CA2017. The event still
// cannot carry both values under one key.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, string from, string to) {
            logger.Information("Moved {Path} to {Path}", from, to);
        }
    }
}
