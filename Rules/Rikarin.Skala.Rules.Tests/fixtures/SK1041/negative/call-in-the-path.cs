public sealed class Slot {
    public int Total;
}

public sealed class Pool {
    readonly Slot slot = new();

    Slot Current() => slot;

    // The call happens twice in the long form and once in the compound one.
    public void Bump() {
        Current().Total = Current().Total + 1;
    }
}
