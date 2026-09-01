using System.Collections.Generic;

// `Dictionary<int, V>` is countable and its indexer takes an `int`, so `map[^1]` compiles and lowers
// to exactly this expression — and reads as an ordinal position the type does not have.
public sealed class Registry {
    public string Lookup(Dictionary<int, string> map) => map[map.Count - 1];
}
