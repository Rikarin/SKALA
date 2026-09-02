// ⚠ A field cannot be set by an object initializer the way an `init` property can.
public sealed class Frozen {
    private int Total { get; init; }

    public Frozen() {
        Total = 1;
    }

    public int Value() => Total;
}
