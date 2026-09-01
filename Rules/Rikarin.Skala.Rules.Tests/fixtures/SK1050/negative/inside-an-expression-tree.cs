using System;
using System.Linq.Expressions;

public abstract class Shape;

public sealed class Circle : Shape;

// A pattern is CS8122 inside an expression tree.
public sealed class Inspector {
    public Expression<Func<Shape, bool>> IsCircle() => shape => shape as Circle != null;
}
