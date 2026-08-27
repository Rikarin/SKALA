public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

// The declaration is not the block's first statement. Moving it into the condition would move it
// across a call, and the rule's whole claim is that nothing moves.
public sealed class Visitor {
    public Node? Visit(Node node) {
        if (node is Binary) {
            System.Console.WriteLine(node);
            var binary = (Binary)node;
            return binary.Left;
        }

        return null;
    }
}
