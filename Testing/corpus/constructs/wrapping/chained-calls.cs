using System.Collections.Generic;
using System.Linq;

class ChainedCalls {
    void Fits(IEnumerable<string> source) {
        var a = source.Where(x => x.Length > 3).Select(x => x);
    }

    void DoesNotFit(IEnumerable<string> source) {
        var b = source.Where(x => x.Length > 3).OrderBy(x => x).Select(x => x.ToUpperInvariant()).ToList().AsReadOnly().Count();
    }

    void PropertyInTheChain(IEnumerable<string> source) {
        var c = source.Where(x => x.Length > 3).ToList().Count.ToString().Trim().Substring(0, 1).ToUpperInvariant().Trim();
    }
}
