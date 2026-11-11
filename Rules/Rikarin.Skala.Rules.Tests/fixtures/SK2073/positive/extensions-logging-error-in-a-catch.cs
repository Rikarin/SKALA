// No CA rule reports this: measured in a probe project at default analysis level and again
// at AnalysisMode=All, nothing fires on a catch that logs without its exception.
namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class Reader {
        public void Run(Microsoft.Extensions.Logging.ILogger logger, string path) {
            try {
                System.Console.WriteLine(path);
            } catch (System.IO.IOException ex) {
                logger.LogError("could not read {Path}", path);
            }
        }
    }
}
