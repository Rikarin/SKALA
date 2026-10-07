// #431: a base constructor that only stores its argument runs no code that could reach the field, so the
// initializer is still dead.
public abstract class Shape {
    protected Shape(int sides) {
        Sides = sides;
    }

    public int Sides { get; }
}

public sealed class Square : Shape {
    readonly int size = 1;

    public Square(int given) : base(4) {
        size = given;
    }

    public int Size => size;
}

public static class Probe {
    public static int Run() => new Square(3).Size * 10 + new Square(2).Sides;
}
