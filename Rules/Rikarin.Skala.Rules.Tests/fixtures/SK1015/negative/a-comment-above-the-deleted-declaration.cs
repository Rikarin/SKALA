// ⚠ The OTHER direction of #325, and the reason this rule's guard was not moved with the other 24.
// The fix folds the cast into the pattern and then deletes the declaration's whole LINE with
// `RewriteGuards.LineSpanOf`, which is `FullSpan` — so the comment below is inside the text the
// edit removes, and the finding must withdraw.
//
// ⚠ This fixture is the sabotage for that decision. Move the guard onto the span question, as the
// other 24 sites were moved, and SK1015 fires here and its fix silently eats the sentence — which
// is strictly worse than the missed finding #302 was about.
public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

public sealed class Visitor {
    public Node? Visit(Node node) {
        if (node is Binary) {
            // the cast is safe because the test above already established the type
            var binary = (Binary)node;
            return binary.Left;
        }

        return null;
    }
}
