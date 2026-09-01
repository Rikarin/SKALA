struct CounterFixture {
    static int total;

    public readonly int Value;

    public CounterFixture(int value) {
        Value = value;
        total++;
    }

    public static int Total => total;
}
