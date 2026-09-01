using System.Collections.Generic;

public sealed class Weights {
    // `EqualityComparer<string>.Default` is ordinal, so these are two keys and the dictionary holds
    // both.
    public static readonly Dictionary<string, int> Table = new() {
        ["alpha"] = 1,
        ["Alpha"] = 2
    };
}
