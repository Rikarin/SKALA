// ⚠ The destructive direction, found while auditing #325. This fix makes TWO edits — it deletes the
// declaration's line AND rewrites the `ContainsKey` call into `TryGetValue` — and only the deletion
// was guarded. A comment inside the call was therefore inside a span the fix replaces, and the fix
// silently deleted it.
using System.Collections.Generic;

public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    public int Read(string key) {
        if (_counts.ContainsKey(/* the caller has already trimmed this */ key)) {
            var count = _counts[key];
            return count * 2;
        }

        return 0;
    }
}
