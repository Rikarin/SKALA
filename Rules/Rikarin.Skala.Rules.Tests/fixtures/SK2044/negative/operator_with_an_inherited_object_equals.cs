class Shape {
    public int Sides { get; init; }

    public override bool Equals(object? other) => other is Shape shape && shape.Sides == Sides;

    public override int GetHashCode() => Sides;
}

sealed class Square : Shape {
    public static bool operator ==(Square? left, Square? right) => Equals(left, right);

    public static bool operator !=(Square? left, Square? right) => !(left == right);
}
