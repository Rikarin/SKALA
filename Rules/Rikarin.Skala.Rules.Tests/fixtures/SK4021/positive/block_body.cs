sealed class BlockBodyFixture {
    public int Use(int value) => Twice(value);

    int Twice(int value) {
        return value * 2;
    }
}
