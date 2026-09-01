struct Point {
    public int X;
    public override bool Equals(object? value) => value is Point p && p.X == X;
    public override int GetHashCode() => X;
}

class C {
    bool M(Point a, Point b) => a.Equals(b);
}
