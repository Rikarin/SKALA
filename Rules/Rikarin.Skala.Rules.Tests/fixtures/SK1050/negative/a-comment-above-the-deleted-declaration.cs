// ⚠ The OTHER direction of #325, on this rule's `as`-then-null-check shape. That fix folds the
// conversion into a pattern and deletes the declaration's whole LINE with `RewriteGuards.LineSpanOf`
// — `FullSpan` — so the comment below is inside the edit and the finding must withdraw.
//
// ⚠ Four of this analyzer's five guards WERE moved to the span question; this one was not, and the
// pair pins the boundary. `positive/a-commented-null-check.cs` is the same code with the comment on
// the `if` condition instead, where the fix touches nothing, and there the rule must fire.
public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

public sealed class Visitor {
    public Node? Visit(Node node) {
        // a null result here means the node was some other kind, not that the walk failed
        var binary = node as Binary;
        if (binary != null) {
            return binary.Left;
        }

        return null;
    }
}
