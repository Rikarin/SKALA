// fixture-option: TargetFramework = net10.0
// ⚠ #515: no LangVersion option, so the harness parses at Preview — and the net10.0 reference set proves
// the compiler as it does for `latest`. Pinned by Probe on the empty copies, which Roslyn 4.8 and 4.11
// built fresh.
using System;
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    static IEnumerable<int> Nothing() {
        yield break;
    }

    public static string Run() {
        int[] fromArray = new int[0].ToArray();
        int[] fromSequence = Nothing().ToArray();
        int[] fromQuery = new[] { 1 }.Where(static value => value > 1).ToArray();
        int[] fromList = new List<int>().ToArray();
        List<int> listCopy = Nothing().ToList();
        return string.Join(
            ",",
            ReferenceEquals(fromArray, Array.Empty<int>()),
            ReferenceEquals(fromSequence, Array.Empty<int>()),
            ReferenceEquals(fromQuery, Array.Empty<int>()),
            ReferenceEquals(fromList, Array.Empty<int>()),
            listCopy.Capacity
        );
    }
}
