// The name ends at the alignment or the format specifier, so these two holes are `Count` and
// `Ratio` — and a parser that read to the closing brace would see four distinct strings and miss a
// real duplicate spelled with two different format specifiers.
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
