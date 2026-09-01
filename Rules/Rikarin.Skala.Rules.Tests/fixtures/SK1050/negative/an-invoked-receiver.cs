public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

// The conversion operand is a call. Moving it into a condition moves a side effect, so the rule
// only ever moves a chain of plain names.
public sealed class Visitor {
    Node? current;

    Node Get() => current ?? new Binary();

    public Node? Visit() {
        var binary = Get() as Binary;
        if (binary != null) {
            return binary.Left;
        }

        return null;
    }
}
