struct PrivateSetterFixture {
    public PrivateSetterFixture(int x) => X = x;

    public int X { get; private set; }
}
