public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

// The receiver is an element access: the test and the cast read it twice today, and an indexer is
// not something the rule may assume returns the same object each time.
public sealed class Visitor {
    public Node? Visit(Node[] nodes, int index) {
        if (nodes[index] is Binary) {
            var binary = (Binary)nodes[index];
            return binary.Left;
        }

        return null;
    }
}
