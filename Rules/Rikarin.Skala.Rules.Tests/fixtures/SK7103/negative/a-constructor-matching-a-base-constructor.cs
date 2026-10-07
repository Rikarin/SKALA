/// <summary>A shape.</summary>
public abstract class Shape {
    /// <summary>Creates a shape with a name.</summary>
    /// <param name="name">The name.</param>
    protected Shape(string name) => Name = name;

    /// <summary>The name.</summary>
    public string Name { get; }
}

/// <summary>A named shape.</summary>
public sealed class Named : Shape {
    /// <inheritdoc />
    public Named(string name) : base(name) { }
}
