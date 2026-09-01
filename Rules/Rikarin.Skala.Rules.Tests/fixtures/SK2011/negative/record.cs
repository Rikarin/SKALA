readonly record struct Point(int X);

class C {
    bool M(Point a, object b) => a.Equals(b);
}
