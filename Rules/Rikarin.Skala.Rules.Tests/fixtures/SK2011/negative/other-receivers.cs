using System;

enum Color {
    Red,
    Blue
}

struct Point {
    public int X;
}

class C {
    bool M(Color a, Color b) => a.Equals(b);
    bool N(Point? a, Point? b) => a.Equals(b);
    bool P(ValueType a, object b) => a.Equals(b);
}
