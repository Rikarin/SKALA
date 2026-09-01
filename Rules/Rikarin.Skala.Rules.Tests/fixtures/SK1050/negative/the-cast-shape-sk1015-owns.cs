public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

// `is T` followed by a cast is SK1015's shape and this rule never restates it.
public sealed class Visitor {
    public Node? Visit(Node node) {
        if (node is Binary) {
            var binary = (Binary)node;
            return binary.Left;
        }

        return null;
    }
}
