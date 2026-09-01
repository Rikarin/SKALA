using System;
using System.Collections.Generic;

public sealed class Weights {
    // ⚠ Two ordinally equal keys, and the rule still declines. Key equality belongs to the
    // comparer, so the analyzer refuses to answer the moment the constructor is given anything at
    // all rather than deciding which argument a comparer arrived through.
    public static readonly Dictionary<string, int> Table = new(StringComparer.Ordinal) {
        ["alpha"] = 1,
        ["alpha"] = 2
    };
}
