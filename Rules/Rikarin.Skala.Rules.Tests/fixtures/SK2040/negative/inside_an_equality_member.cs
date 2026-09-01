sealed class Node {
    public Node? Next { get; init; }

    public override bool Equals(object? other) => other is Node node && Next == node.Next;

    public override int GetHashCode() => 0;
}
