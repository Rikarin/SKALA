// A static method is not ordered against anything.
static class Config {
    public static readonly int Value = Compute();

    static int Compute() => 42;
}
