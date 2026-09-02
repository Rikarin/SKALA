// ⚠ #302's shape (#325). The guard asked over the local declaration's FULL span, so a comment
// above it declined the finding — while the fix rewrites `statement.Declaration.Span`, which is
// narrower than the statement twice over: it excludes both the leading trivia and the trailing `;`.
using System.Collections.Generic;

public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public void Drop(string key) {
        // the call is the point; the name it is assigned to is not
        var removed = entries.Remove(key);
    }
}
