namespace Contoso.Design;

// `this is Shape` is always true. That is a redundancy rather than an inverted dependency: no subclass
// appears in it, so adding one changes nothing here. ⚠ It declines through the base-type walk rather
// than through an identity check: the walk starts at `Shape.BaseType`, so `Shape` is already past it.
// A separate `target != self` guard was written first and no sabotage could turn it red.
public class Shape {
    public bool Check() => this is Shape;
}

public sealed class Circle : Shape;
