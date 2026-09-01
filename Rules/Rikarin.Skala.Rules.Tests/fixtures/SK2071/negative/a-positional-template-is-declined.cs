// Indices are `string.Format` semantics, not property names; `{0}` twice is one value rendered
// twice and nothing is lost. CA2253 is the rule that says not to write one.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, int count) {
            logger.Information("{0} and again {0}", count);
        }
    }
}
