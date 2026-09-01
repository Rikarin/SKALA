public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

public sealed class Visitor {
    public Node? Visit(Node node) {
        var binary = node as Binary;
        if (binary != null) {
            return binary.Left;
        }

        return null;
    }
}
