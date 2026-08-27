public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

// `as` yields null rather than throwing, and its declared type is nullable. It is a different
// conversion from the one the test proved and it is not this rule's shape.
public sealed class Visitor {
    public Node? Visit(Node node) {
        if (node is Binary) {
            var binary = node as Binary;
            return binary?.Left;
        }

        return null;
    }
}
