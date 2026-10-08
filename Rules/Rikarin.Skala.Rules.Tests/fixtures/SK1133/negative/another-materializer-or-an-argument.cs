// fixture-option: LangVersion = 14
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Others {
    public HashSet<string> Unique(IEnumerable<string> names) {
        HashSet<string> unique = names.ToHashSet(StringComparer.Ordinal);
        return unique;
    }

    public Dictionary<string, int> Index(IEnumerable<string> names) {
        Dictionary<string, int> index = names.ToDictionary(static name => name, static name => name.Length);
        return index;
    }

    public int[] Static(IEnumerable<int> source) {
        int[] copied = Enumerable.ToArray(source);
        return copied;
    }
}
