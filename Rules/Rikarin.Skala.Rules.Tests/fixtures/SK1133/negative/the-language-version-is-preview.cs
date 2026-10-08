// No fixture-option: the harness parses at Preview, which no compiler pins either.
using System.Collections.Generic;
using System.Linq;

public sealed class Preview {
    public int[] Copy(IEnumerable<int> source) {
        int[] copied = source.ToArray();
        return copied;
    }
}
