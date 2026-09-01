using System.Collections.Generic;

public sealed class Weights {
    // Only the key side is unique by construction.
    public static readonly Dictionary<string, int> Table = new() {
        ["alpha"] = 1,
        ["beta"] = 1,
        ["gamma"] = 1
    };
}
