using System.Collections.Generic;

public sealed class Weights {
    // Two collections, one key each. The rule is scoped to one initializer, because that is the
    // scope in which a repeat is a mistake.
    public static readonly Dictionary<string, int> Incoming = new() { ["alpha"] = 1 };

    public static readonly Dictionary<string, int> Outgoing = new() { ["alpha"] = 2 };
}
