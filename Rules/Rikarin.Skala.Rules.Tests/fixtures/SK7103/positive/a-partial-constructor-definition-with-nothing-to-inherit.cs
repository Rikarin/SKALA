/// <summary>A shape.</summary>
public abstract class Shape {
    /// <summary>Creates a shape with a name.</summary>
    /// <param name="name">The name.</param>
    protected Shape(string name) => Name = name;

    /// <summary>The name.</summary>
    public string Name { get; }
}

/// <summary>A circle.</summary>
public sealed partial class Circle : Shape {
    // #401: the `<inheritdoc/>` is on the definition, the half Roslyn's driver never visits, and no
    // base constructor takes (double).
    /// <inheritdoc />
    public partial Circle(double radius);

    public partial Circle(double radius) : base("circle") => Radius = radius;

    /// <summary>The radius.</summary>
    public double Radius { get; }
}
