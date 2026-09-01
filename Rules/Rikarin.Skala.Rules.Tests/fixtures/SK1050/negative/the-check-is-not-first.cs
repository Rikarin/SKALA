public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

// The null check is the second operand of the `&&`, so it is not the first thing the condition
// does and the declaration cannot become the thing that introduces the name.
public sealed class Visitor {
    public Node? Visit(Node node, bool enabled) {
        var binary = node as Binary;
        if (enabled && binary != null) {
            return binary.Left;
        }

        return null;
    }
}
