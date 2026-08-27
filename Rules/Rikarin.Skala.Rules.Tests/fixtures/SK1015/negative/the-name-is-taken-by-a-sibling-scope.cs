public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

// `binary` moves into the method block, where the earlier `if` block already declares it one scope
// down: legal as two cousins today, CS0136 afterwards.
public sealed class Visitor {
    public Node? Visit(Node node, bool first) {
        if (first) {
            var binary = 1;
            System.Console.WriteLine(binary);
        }

        if (node is Binary) {
            var binary = (Binary)node;
            return binary.Left;
        }

        return null;
    }
}
