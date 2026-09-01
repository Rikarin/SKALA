struct PointFixture {
    public readonly int X;
    public readonly int Y;

    public PointFixture(int x, int y) {
        X = x;
        Y = y;
    }

    public int Sum => X + Y;
}
