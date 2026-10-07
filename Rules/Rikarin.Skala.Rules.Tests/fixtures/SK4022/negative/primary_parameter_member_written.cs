struct Point {
    public int X;
}

// Writing a field of a captured struct-typed parameter writes the parameter.
struct Anchor(Point origin) {
    public void Move(int x) => origin.X = x;

    public int X => origin.X;
}
