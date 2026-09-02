// ⚠ The destructive direction, found while auditing #325 and not by any existing fixture. The fix
// makes TWO edits: it deletes the declaration's line, and it REWRITES `node is Binary` into a
// pattern. Only the first was guarded, so a comment inside the test itself was inside a span the fix
// replaces — and the fix silently deleted it.
//
// ⚠ That is the failure the guard exists to prevent, in the opposite polarity to #302: not a rule
// gone quiet on documented code, but a fix destroying text a person wrote. It is the worse of the
// two, because a missed finding is recoverable and a deleted sentence is not.
public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

public sealed class Visitor {
    public Node? Visit(Node node) {
        if (node is /* the only kind with children */ Binary) {
            var binary = (Binary)node;
            return binary.Left;
        }

        return null;
    }
}
