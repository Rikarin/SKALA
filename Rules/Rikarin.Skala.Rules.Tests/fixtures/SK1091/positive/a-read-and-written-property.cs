public sealed class Counter {
    private int Total { get; set; }

    public void Add(int amount) {
        Total += amount;
    }

    public int Value() => Total;
}
