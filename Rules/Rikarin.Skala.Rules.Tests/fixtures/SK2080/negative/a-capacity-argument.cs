using System.Collections.Generic;

public sealed class Weights {
    // ⚠ The documented cost of declining on *any* argument: a capacity is not a comparer and this
    // is a true finding the rule gives up. Stated rather than hidden.
    public static readonly Dictionary<string, int> Table = new(8) {
        ["alpha"] = 1,
        ["alpha"] = 2
    };
}
