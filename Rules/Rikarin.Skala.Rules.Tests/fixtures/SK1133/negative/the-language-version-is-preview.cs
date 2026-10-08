// No fixture-option: the harness parses at Preview, which no compiler pins either, against the test
// host's own runtime assemblies — System.Private.CoreLib, which no build references (#515), and which is
// what `--load=loose` compiles against too.
using System.Collections.Generic;
using System.Linq;

public sealed class Preview {
    public int[] Copy(IEnumerable<int> source) {
        int[] copied = source.ToArray();
        return copied;
    }
}
