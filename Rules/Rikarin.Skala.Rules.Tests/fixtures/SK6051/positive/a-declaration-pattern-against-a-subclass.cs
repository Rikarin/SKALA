namespace Contoso.Design;

public class Node {
    public string Describe() => this is Leaf leaf ? leaf.Text : "branch";
}

public sealed class Leaf : Node {
    public string Text { get; init; } = string.Empty;
}
