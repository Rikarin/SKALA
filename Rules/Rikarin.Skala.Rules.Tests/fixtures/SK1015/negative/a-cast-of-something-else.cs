public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

public sealed class Visitor {
    public Node? Visit(Node node, object other) {
        if (node is Binary) {
            var binary = (Binary)other;
            return binary.Left;
        }

        return null;
    }
}
