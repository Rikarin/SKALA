public abstract class Node;

public sealed class Binary : Node {
    public Node? Left;
}

public sealed class Visitor {
    Node? _current;

    public Node? Visit() {
        if (_current is Binary) {
            var binary = (Binary)_current;
            return binary.Left;
        }

        return null;
    }
}
