/// <summary>A shape.</summary>
public abstract class Shape {
    /// <summary>Creates a shape with a name.</summary>
    /// <param name="name">The name.</param>
    protected Shape(string name) => Name = name;

    /// <summary>The name.</summary>
    public string Name { get; }
}

/// <summary>A circle.</summary>
public sealed class Circle : Shape {
    // Roslyn looks for a base constructor taking (double), and there is none.
    /// <inheritdoc />
    public Circle(double radius) : base("circle") => Radius = radius;

    /// <summary>The radius.</summary>
    public double Radius { get; }
}
