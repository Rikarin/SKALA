sealed class LocalFunctionUsesFieldFixture {
    readonly int factor = 2;

    public int Use(int value) => Scale(value);

    int Scale(int value) {
        int Apply(int input) => input * factor;

        return Apply(value);
    }
}
