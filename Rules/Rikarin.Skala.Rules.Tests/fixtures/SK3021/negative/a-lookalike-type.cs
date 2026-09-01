namespace Vendor;

public struct SpinLock {
    public void Enter() { }
}

public sealed class Counter {
    // ⚠ Somebody else's `SpinLock`. The type is resolved rather than name-matched, so stripping
    // `readonly` here would be a change made for no reason at all.
    readonly SpinLock gate;

    public void Take() => gate.Enter();
}
