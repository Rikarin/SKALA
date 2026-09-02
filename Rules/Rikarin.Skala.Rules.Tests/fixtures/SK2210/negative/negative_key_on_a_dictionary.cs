// ⚠ `map[-1]` is an ordinary key lookup, not an out-of-range access, and a dictionary is excluded
// outright for that reason. The check is the declared contract, not the parameter type: an `int`
// indexer on `IDictionary<int, T>` is a key and an `int` indexer on `IList<T>` is a position.
using System.Collections.Generic;

class C {
    string Lookup(Dictionary<int, string> map) => map[-1];

    string Read(IReadOnlyDictionary<int, string> map) => map[-1];
}
