// ⚠ The OTHER direction of #325, on this rule's read-after-ContainsKey shape. That fix rewrites the
// `if` AND deletes the following declaration's whole LINE with `RewriteGuards.LineSpanOf`, which is
// `FullSpan` — so the comment below is inside the edit and the finding must withdraw.
//
// ⚠ Both questions are live in this one analyzer, which is why it was split rather than switched:
// the write-when-absent shape rewrites only `statement.Span` and was moved, and
// `positive/a-commented-write-when-absent.cs` pins that a comment above the `if` no longer silences
// it. Moving THIS site too would make the fix eat the sentence below.
using System.Collections.Generic;

public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    public int Read(string key) {
        if (_counts.ContainsKey(key)) {
            // the second lookup is the one that costs, and it is the one being removed
            var count = _counts[key];
            return count * 2;
        }

        return 0;
    }
}
