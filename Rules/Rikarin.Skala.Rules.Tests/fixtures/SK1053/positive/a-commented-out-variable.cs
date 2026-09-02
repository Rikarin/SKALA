// ⚠ #302's shape (#325), on this rule's `out var` branch — a second call site of the same guard.
// It over-reached twice: the guard asked over the ARGUMENT's full span while the fix rewrites only
// the `out var value` declaration inside it. So a comment before `out` declined the finding, and so
// did one between `out` and `var` — which the fix would have preserved either way.
using System.Collections.Generic;

public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public bool Has(string key) => entries.TryGetValue(
        key,
        // nobody reads this one; only the boolean matters
        out var value
    );
}
