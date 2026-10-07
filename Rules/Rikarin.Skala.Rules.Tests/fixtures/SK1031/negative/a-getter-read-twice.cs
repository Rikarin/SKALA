// ⚠ The guard and the assignment read `Current` twice; `owner.Current?.Label = "x"` reads it once.
// Measured for #412's audit with a getter that answers null the second time: a
// NullReferenceException before, none after. A property with a body is not storage.
public sealed class Node {
    public string? Label;
}

public sealed class Owner {
    int reads;

    public Node? Current {
        get {
            reads++;
            return reads == 1 ? new Node() : null;
        }
    }

    public void Touch() {
        if (Current is not null) {
            Current.Label = "x";
        }
    }
}
