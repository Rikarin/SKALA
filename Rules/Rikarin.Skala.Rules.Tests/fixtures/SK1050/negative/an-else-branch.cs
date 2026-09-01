public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

// In the `else` the local is legibly null and the pattern variable would be unassigned.
public sealed class Visitor {
    public Node? Visit(Node node) {
        var binary = node as Binary;
        if (binary != null) {
            return binary.Left;
        } else {
            return null;
        }
    }
}
