// ⚠ Serilog binds values to *holes* in order, not to distinct names, so two holes spelled the same
// with two arguments is arity-correct. It is SK2071's finding, not this one's.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, int before, int after) {
            logger.Information("{Count} then {Count}", before, after);
        }
    }
}
