public sealed class Documented {
    private int Total { get; /* set nowhere else */ set; }

    public int Value() {
        Total = 1;
        return Total;
    }
}
