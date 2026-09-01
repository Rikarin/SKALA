struct Coord {
    public int X;

    public bool Equals(Coord other) => other.X == X;

    public override bool Equals(object? other) => other is Coord coord && Equals(coord);

    public override int GetHashCode() => X;
}
