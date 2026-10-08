// fixture-option: LangVersion = 14
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Deferred {
    public Func<int[]> Later(IEnumerable<int> source) {
        Func<int[]> later = () => source.ToArray();
        return later;
    }
}
