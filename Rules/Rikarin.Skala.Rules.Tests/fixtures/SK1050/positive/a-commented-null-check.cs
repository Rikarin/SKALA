// ⚠ #302's shape (#325), on this rule's `as`-then-null-check branch, and the one place in the
// analyzer where BOTH questions are live at once. The declaration `var binary = node as Binary;` is
// guarded by the NODE question and must be — the fix deletes its whole line with `LineSpanOf`, so a
// comment above it really would be eaten. The `if` condition beside it is guarded separately and
// only ever has its own span rewritten, so the comment below silenced the rule for no reason.
public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

public sealed class Visitor {
    public Node? Visit(Node node) {
        var binary = node as Binary;
        if (
            // only the binary case has anything to walk into
            binary != null) {
            return binary.Left;
        }

        return null;
    }
}
