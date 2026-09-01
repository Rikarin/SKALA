struct SizeFixture {
    public SizeFixture(int width) {
        Width = width;
        Height = 0;
    }

    public int Width { get; }

    public int Height { get; init; }
}
