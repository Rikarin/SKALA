// fixture-option: LangVersion = 14
// ⚠ `few.ToArray()` boxes the struct into `Enumerable.ToArray`, which answers an empty sequence with
// `Array.Empty<int>()`. `[..few]` is not that call: the compiler enumerates a struct into a fresh
// `List<int>` and copies it out (decompiled, #512), so the empty copy is a new array. Pinned by Probe.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public struct Few : IEnumerable<int> {
    public IEnumerator<int> GetEnumerator() {
        yield break;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class Probe {
    public static bool Run() {
        int[] copied = new Few().ToArray();
        return ReferenceEquals(copied, Array.Empty<int>());
    }
}
