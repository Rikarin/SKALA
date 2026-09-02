using System.Collections.Immutable;
using System.Diagnostics;

// ⚠ The reason the namespaces are listed exactly rather than matched by prefix.
// `ImmutableList<T>.Add` and `ImmutableDictionary<K, V>.Remove` return a new collection and mutate
// nothing, so a prefix test on "System.Collections" would make every one of them a false positive.
public sealed class Tracker {
    readonly ImmutableList<int> items = ImmutableList<int>.Empty;

    readonly ImmutableDictionary<int, string> names = ImmutableDictionary<int, string>.Empty;

    public void Check() {
        Debug.Assert(items.Add(1).Count == 1);
        Debug.Assert(names.Remove(1).IsEmpty);
    }
}
