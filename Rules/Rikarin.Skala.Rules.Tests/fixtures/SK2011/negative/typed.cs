struct Point {
    public int X;
    public bool Equals(Point value) => value.X == X;
}

class C {
    bool M(Point a, Point b) => a.Equals(b);
}
