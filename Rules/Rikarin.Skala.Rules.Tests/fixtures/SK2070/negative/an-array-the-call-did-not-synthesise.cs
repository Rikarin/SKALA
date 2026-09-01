// A `params` parameter handed an existing array has a length this analysis cannot see. Counting the
// array itself as one value would report every such call as a mismatch.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, object[] values) {
            logger.Information("Loaded {Count} of {Total}", values);
        }
    }
}
