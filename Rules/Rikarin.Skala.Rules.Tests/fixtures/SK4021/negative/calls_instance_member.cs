sealed class CallsInstanceMemberFixture {
    public int Seed => 2;

    public int Use(int value) => Twice(value);

    int Twice(int value) => value * Seed;
}
