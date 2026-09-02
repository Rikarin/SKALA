// ⚠ The exclusion that is load-bearing rather than cautious. An `out` parameter *must* be
// assigned before it is read — that is its contract — so data flow reports every correct one as
// having its incoming value discarded. Measured, not assumed: this shape was run through
// AnalyzeDataFlow and came back with the parameter written inside and absent from DataFlowsIn,
// which is precisely the defect's signature.
namespace Fixtures {
    sealed class Parser {
        public bool TryParse(string text, out int value) {
            value = text.Length;
            return true;
        }
    }
}
