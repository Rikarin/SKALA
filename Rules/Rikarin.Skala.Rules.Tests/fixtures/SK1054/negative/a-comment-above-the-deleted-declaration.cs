// ⚠ The OTHER direction of #325, and the reason this rule's guard kept the node question. The fix
// inlines the `out` variable and then deletes the standalone declaration's whole LINE with
// `RewriteGuards.LineSpanOf`, which is `FullSpan` — so the comment below is inside the edit and the
// finding must withdraw.
//
// ⚠ Move this guard onto the span question and the rule fires here, deleting the sentence with the
// declaration it explains.
using System.Collections.Generic;

public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public int Get(string key) {
        // zero is a legitimate stored value, so the flag is what decides, not the number
        int value;
        if (entries.TryGetValue(key, out value)) {
            return value;
        }

        return 0;
    }
}
