struct Point {
    public int X;

    public override bool Equals(object? other) => other is Point point && point.X == X;

    public override int GetHashCode() => base.GetHashCode();
}
