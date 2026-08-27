public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

public sealed class Visitor {
    public Node? Visit(Node node) {
        if (node is Binary) {
            Binary binary = (Binary)node;
            return binary.Left;
        }

        return null;
    }
}
