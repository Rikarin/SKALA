sealed class UsesFieldFixture {
    readonly int factor = 2;

    public int Use(int value) => Scale(value);

    int Scale(int value) => value * factor;
}
