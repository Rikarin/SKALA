// ⚠ Comparison is ordinal. `{Count}` and `{count}` are two properties to every sink there is, so no
// value is lost and calling them a duplicate would be an opinion about naming dressed as a defect.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, int before, int after) {
            logger.Information("{Count} then {count}", before, after);
        }
    }
}
