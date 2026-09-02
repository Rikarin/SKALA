// ⚠ A private field assigned and never read is CS0414.
public sealed class WriteOnly {
    private int Total { get; set; }

    public void Set(int value) {
        Total = value;
    }
}
