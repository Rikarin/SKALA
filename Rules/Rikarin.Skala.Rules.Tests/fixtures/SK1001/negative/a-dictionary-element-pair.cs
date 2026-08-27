using System.Collections.Generic;

// `{ k, v }` is a complex element initializer calling a two-argument `Add`. There is no
// collection-expression spelling of it.
public sealed class Names {
    public Dictionary<string, int> All() {
        Dictionary<string, int> counts = new Dictionary<string, int> { { "a", 1 } };
        return counts;
    }
}
