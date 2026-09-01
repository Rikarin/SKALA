sealed class ThisReceiverFixture {
    public int Use(int value) => this.Twice(value);

    int Twice(int value) => value * 2;
}
