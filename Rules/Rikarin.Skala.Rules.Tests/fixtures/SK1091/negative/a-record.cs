public sealed record Thing {
    private int Total { get; set; }

    public int Value() {
        Total = 1;
        return Total;
    }
}
