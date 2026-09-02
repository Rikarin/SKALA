public sealed class Documented {
    /// <summary>How many have been counted.</summary>
    private int Total { get; set; }

    public void Add(int amount) {
        Total += amount;
    }

    public int Value() => Total;
}
