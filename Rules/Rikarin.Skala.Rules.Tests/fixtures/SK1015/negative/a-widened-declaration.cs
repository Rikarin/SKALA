public abstract class Node;

public sealed class Binary : Node;

// A pattern variable is bound to the tested type, so `object binary` cannot survive the rewrite.
public sealed class Visitor {
    public object? Visit(Node node) {
        if (node is Binary) {
            object binary = (Binary)node;
            return binary;
        }

        return null;
    }
}
