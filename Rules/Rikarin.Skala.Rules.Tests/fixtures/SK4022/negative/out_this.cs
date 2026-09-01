struct OutThisFixture {
    public readonly int X;

    public OutThisFixture(int x) => X = x;

    public void Clear() => Fill(out this);

    static void Fill(out OutThisFixture value) => value = default;
}
