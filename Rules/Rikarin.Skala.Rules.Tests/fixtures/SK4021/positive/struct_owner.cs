struct StructOwnerFixture {
    public int Value;

    public int Use() => Double(Value);

    int Double(int input) => input * 2;
}
