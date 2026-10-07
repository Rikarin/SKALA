// An auto-property's getter reads only its backing field, so reading the receiver once or twice is
// the same; the fix's result is pinned by Probe (#423).
public sealed class Node {
    public string Label = "label";
}

public sealed class Owner {
    public Node? Current { get; set; }

    public string? LabelOf() => Current != null ? Current.Label : null;
}

public static class Probe {
    public static int Run() {
        var owner = new Owner { Current = new Node() };
        var label = owner.LabelOf();
        return label == null ? -1 : label.Length;
    }
}
