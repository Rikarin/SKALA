// The name ends at the alignment (`,`) or the format specifier (`:`), whichever comes first.
namespace Serilog {
    interface ILogger {
        void Information(string messageTemplate, params object[] propertyValues);
    }
}

namespace Fixtures {
    sealed class Worker {
        public void Run(Serilog.ILogger logger, int count, double ratio) {
            logger.Information("{Count,10} at {Ratio:P2}", count, ratio);
        }
    }
}
