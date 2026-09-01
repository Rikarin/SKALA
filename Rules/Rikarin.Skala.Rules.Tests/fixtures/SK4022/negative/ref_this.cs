struct RefThisFixture {
    public readonly int X;

    public RefThisFixture(int x) => X = x;

    public void Pass() => Consume(ref this);

    static void Consume(ref RefThisFixture value) => value = default;
}
