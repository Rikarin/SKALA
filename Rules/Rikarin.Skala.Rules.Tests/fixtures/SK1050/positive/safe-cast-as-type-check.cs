public abstract class Shape;

public sealed class Circle : Shape;

public sealed class Inspector {
    public bool IsCircle(Shape shape) => shape as Circle != null;
}
