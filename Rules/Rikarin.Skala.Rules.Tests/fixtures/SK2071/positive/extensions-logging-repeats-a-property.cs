// ⚠ CA2017 counts holes, not names, so it is silent on exactly this call — measured in a probe
// project at default analysis level and at AnalysisMode=All. The concept has no host anywhere,
// which is why this rule covers Microsoft.Extensions.Logging and SK2070 does not.
namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class Worker {
        public void Run(Microsoft.Extensions.Logging.ILogger logger, int before, int after) {
            logger.LogInformation("{Count} then {Count}", before, after);
        }
    }
}
