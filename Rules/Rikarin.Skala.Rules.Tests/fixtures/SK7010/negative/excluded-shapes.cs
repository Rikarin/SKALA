// ⚠ The exclusions rules.json names: accessors, explicit interface implementations, operators,
// finalizers and `record` positional members are not things a person writes a `<summary>` for.

/// <summary>Something with a name.</summary>
public interface INamed {
    /// <summary>The name.</summary>
    string Name { get; }
}

/// <summary>A point.</summary>
public readonly record struct Point(int X, int Y) {
    /// <summary>Adds two points.</summary>
    public static Point operator +(Point left, Point right) => new Point(left.X + right.X, left.Y + right.Y);
}

/// <summary>A named thing whose name comes from the interface.</summary>
public sealed class Named : INamed {
    /// <summary>The name, with an explicitly implemented twin.</summary>
    public string Name { get; } = "x";

    string INamed.Name => Name;

    ~Named() { }
}
