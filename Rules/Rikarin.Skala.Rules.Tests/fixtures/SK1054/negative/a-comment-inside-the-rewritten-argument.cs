// ⚠ The destructive direction, found while auditing #325. This fix makes TWO edits — it deletes the
// standalone declaration's line AND rewrites the `out` argument to carry the declaration inline —
// and only the deletion was guarded. A comment inside the argument was inside a span the fix
// replaces, and the fix silently deleted it.
//
// ⚠ The comment has to sit INSIDE the argument, after the `out`. Written before the `out` it lands
// in the argument's LEADING trivia, which is outside `target.Span` — the fix preserves it there and
// the rule correctly fires. That first draft of this fixture failed for exactly that reason, which
// is the same span/full-span distinction this whole audit is about, one level down.
using System.Collections.Generic;

public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public int Get(string key) {
        int value;
        if (entries.TryGetValue(key, out /* zero is a real stored value */ value)) {
            return value;
        }

        return 0;
    }
}
