public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

public sealed class Visitor {
    public Node? Visit(Node node) {
        if (node is Binary binary) {
            return binary.Left;
        }

        return null;
    }
}
