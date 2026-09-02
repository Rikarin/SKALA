// ⚠ `map[-1]` is an ordinary key lookup, not an out-of-range access. The check is the declared
// contract, not the parameter type: an `int` indexer on `IDictionary<int, T>` is a key and an `int`
// indexer on `IList<T>` is a position.
//
// ⚠ The explicit dictionary exclusion is *not* what saves this file, and a sabotage is what showed
// it: removing that test left this fixture green. `Dictionary<int, V>` implements `IDictionary` and
// `IReadOnlyDictionary` and neither `IList` nor `IReadOnlyList`, so the positive whitelist already
// declines it. The exclusion is kept as the statement of intent, for the day a type implements both.
using System.Collections.Generic;

class C {
    string Lookup(Dictionary<int, string> map) => map[-1];

    string Read(IReadOnlyDictionary<int, string> map) => map[-1];
}
