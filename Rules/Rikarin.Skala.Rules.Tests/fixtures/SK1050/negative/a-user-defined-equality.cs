public abstract class Shape;

// `!=` here is whatever the type says it is; a pattern is always the reference test.
public sealed class Circle : Shape {
    public static bool operator ==(Circle? left, Circle? right) => true;

    public static bool operator !=(Circle? left, Circle? right) => false;

    public override bool Equals(object? other) => other is Circle;

    public override int GetHashCode() => 0;
}

public sealed class Inspector {
    public bool IsCircle(Shape shape) => shape as Circle != null;
}
