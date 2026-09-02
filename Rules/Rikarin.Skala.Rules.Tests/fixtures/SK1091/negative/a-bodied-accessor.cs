// ⚠ This is SK1003's shape and not this one: a separate field with a hand-written property
// over it. The two rules partition the space rather than sharing it.
public sealed class Backed {
    int total;

    private int Total {
        get => total;
        set => total = value;
    }

    public int Value() {
        Total = 1;
        return Total;
    }
}
