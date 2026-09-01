public abstract class Node;

public sealed class Binary : Node;

// The declaration is `Node`, the test is `Binary`. A declaration pattern binds the variable to the
// tested type, so the rewrite would narrow a variable the author deliberately widened.
public sealed class Visitor {
    public bool Visit(Node node) {
        Node? binary = node as Binary;
        if (binary != null) {
            return binary is Binary;
        }

        return false;
    }
}
