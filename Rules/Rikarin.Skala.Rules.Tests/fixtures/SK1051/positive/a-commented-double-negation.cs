// ⚠ #302's shape (#325). The guard asked over the pattern's FULL span, so a comment inside the
// `if (` — explaining why the negation is doubled — silenced the rule. ⚠ This site also asked the
// same question twice, the node walk beside a text scan over `FullSpan`; both are now one span
// question. The fix rewrites only the pattern and leaves the comment above it.
public sealed class Gate {
    public bool Open(int value) {
        if (value is
            // doubled while the third condition was being written, and never unwound
            not not > 0) {
            return true;
        }

        return false;
    }
}
