namespace Contoso.Design;

// `this is not null`, `this is { }` and a property pattern with no type name test the instance rather
// than the hierarchy. None of them names a subclass, so none of them is the shape.
public class Shape {
    public int Sides { get; init; }

    public bool Ready() => this is { Sides: > 0 } && this is not null;
}

public sealed class Circle : Shape;
