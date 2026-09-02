// A struct is sealed by construction, so its runtime type is its static one and the question is
// decidable without any assumption about what is in the collection.
using System;
using System.Collections.Generic;
using System.Linq;

struct Pair {
    public int A { get; set; }

    public int B { get; set; }
}

class C {
    void SortArray(Pair[] pairs) => Array.Sort(pairs);

    IEnumerable<int> ByPair(IEnumerable<int> items, Func<int, Pair> key) => items.OrderBy(key);
}
