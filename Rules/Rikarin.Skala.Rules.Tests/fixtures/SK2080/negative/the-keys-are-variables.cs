using System.Collections.Generic;

public sealed class Weights {
    // ⚠ `first` and `second` may well hold the same string at run time, and that is exactly why the
    // rule says nothing: a table built from names is the ordinary shape, and only a constant is
    // decidable.
    public static Dictionary<string, int> Build(string first, string second) =>
        new() {
            [first] = 1,
            [second] = 2
        };
}
