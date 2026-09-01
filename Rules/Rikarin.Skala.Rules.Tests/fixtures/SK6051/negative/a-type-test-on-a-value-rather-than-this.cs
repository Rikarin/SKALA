namespace Contoso.Design;

// ⚠ Only `this` written out. A local or a parameter holding an instance of the same hierarchy is an
// ordinary type test on an ordinary value — a visitor, an equality check, a dispatcher. What makes the
// finding a design smell is that the *type* is asking about itself, and that is a syntactic property.
public class Shape {
    public bool Same(Shape other) {
        var self = this;
        return other is Circle || self is Circle;
    }
}

public sealed class Circle : Shape;
