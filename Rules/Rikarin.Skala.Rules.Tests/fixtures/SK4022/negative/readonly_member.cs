struct ReadonlyMemberFixture {
    public readonly int X;

    public ReadonlyMemberFixture(int x) => X = x;

    public readonly int Get() => X;
}
