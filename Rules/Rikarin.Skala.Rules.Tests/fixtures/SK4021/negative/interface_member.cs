interface IInterfaceMemberFixture {
    int Use(int value) => Twice(value);

    private int Twice(int value) => value * 2;
}
