namespace Contoso.Design;

// The other part may declare the abstract member, or may not exist yet because a generator writes it.
public abstract partial class Pipeline {
    public string Name { get; init; } = string.Empty;
}
