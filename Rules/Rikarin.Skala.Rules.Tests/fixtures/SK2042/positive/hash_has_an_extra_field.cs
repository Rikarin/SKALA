sealed class Node {
    readonly int id;

    readonly string label;

    public Node(int id, string label) {
        this.id = id;
        this.label = label;
    }

    public override bool Equals(object? other) => other is Node node && node.id == id;

    public override int GetHashCode() => (id * 31) + label.Length;
}
