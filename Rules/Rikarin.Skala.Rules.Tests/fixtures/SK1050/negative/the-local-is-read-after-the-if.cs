public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

// `binary` is read below the `if`. A pattern variable is declared there and is not definitely
// assigned afterwards, so the same read would be CS0165.
public sealed class Visitor {
    public Node? Visit(Node node) {
        var binary = node as Binary;
        if (binary != null) {
            return binary.Left;
        }

        System.Console.WriteLine(binary is null);
        return null;
    }
}
