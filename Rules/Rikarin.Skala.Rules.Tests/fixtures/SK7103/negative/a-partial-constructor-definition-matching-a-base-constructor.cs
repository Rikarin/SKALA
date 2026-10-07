/// <summary>A shape.</summary>
public abstract class Shape {
    /// <summary>Creates a shape with a name.</summary>
    /// <param name="name">The name.</param>
    protected Shape(string name) => Name = name;

    /// <summary>The name.</summary>
    public string Name { get; }
}

/// <summary>A named shape.</summary>
public sealed partial class Named : Shape {
    // #401: read on the definition now, and resolved: the base has a (string) constructor.
    /// <inheritdoc />
    public partial Named(string name);

    public partial Named(string name) : base(name) { }
}
