partial class PartialOwnerFixture {
    public int Use(int value) => Twice(value);

    int Twice(int value) => value * 2;
}
