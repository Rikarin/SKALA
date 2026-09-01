struct Point {
    public int X;
}

class C {
    bool Same(Point left, Point right) => ReferenceEquals(left, right);
}
