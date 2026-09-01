// ⚠ `{@Order}` and `{Order}` are the same property: the sigil selects how the value is captured,
// not what it is called. A parser that keeps it sees two names where the logger sees one.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, object order, object summary) {
            logger.Information("{@Order} summarised as {Order}", order, summary);
        }
    }
}
