// There is no template to read. CA2254 is the rule for a template that varies between calls.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, string template, string from, string to) {
            logger.Information(template, from, to);
        }
    }
}
