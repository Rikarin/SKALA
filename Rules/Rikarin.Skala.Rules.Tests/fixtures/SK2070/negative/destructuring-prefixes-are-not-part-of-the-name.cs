// Serilog's `@` (destructure) and `$` (stringify) are hole syntax, not name characters.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, object order, object customer) {
            logger.Information("{@Order} for {$Customer}", order, customer);
        }
    }
}
