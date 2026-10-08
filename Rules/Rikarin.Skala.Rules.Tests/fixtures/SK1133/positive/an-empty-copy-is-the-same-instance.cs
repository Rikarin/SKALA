// fixture-option: LangVersion = 14
// ⚠ #512: `Enumerable.ToArray` of an empty array, sequence or query is `Array.Empty<T>()`, and so is the
// spread from Roslyn 5.0 on, because it is the same call. Roslyn 4.8 and 4.11 built a fresh array —
// the identity #425 guards for SK1001. Pinned by Probe, with the List<T> case that is fresh on both sides.
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
