sealed class UsesPropertyFixture {
    int Factor { get; } = 2;

    public int Use(int value) => Scale(value);

    int Scale(int value) => value * Factor;
}
