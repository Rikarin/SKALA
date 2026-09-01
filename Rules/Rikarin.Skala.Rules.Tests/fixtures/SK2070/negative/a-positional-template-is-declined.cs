// ⚠ `{0} {1}` is Serilog's `string.Format` mode: arity is max-index-plus-one rather than a count,
// and a template mixing indices with names has semantics no rule should guess at. CA2253 is the
// rule that says not to write one; this one declines to judge it.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, int count) {
            logger.Information("{0} and {1}", count);
            logger.Information("{0} and {Named}", count);
        }
    }
}
