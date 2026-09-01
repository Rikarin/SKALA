struct AssignsThisFixture {
    public readonly int X;

    public AssignsThisFixture(int x) => X = x;

    public void Reset() => this = default;
}
