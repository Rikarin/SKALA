// ⚠ #302's shape (#325), on this rule's second shape only. The guard asked over the `if`
// statement's FULL span, so a comment above the `if` declined the finding — while this fix replaces
// `statement.Span`, which starts at the `if` keyword, and never reaches the line above.
//
// ⚠ The rule's OTHER shape keeps the node question and must: there the fix also deletes the
// following declaration's whole line with `LineSpanOf`, so the comment above it really would go.
using System.Collections.Generic;

public sealed class Registry {
    readonly Dictionary<string, string> _names = new();

    public void Remember(string key, string name) {
        // only the first writer for a key wins
        if (!_names.ContainsKey(key)) {
            _names[key] = name;
        }
    }
}
