public static class Shared {
    public static readonly object Gate = new object();
}

public sealed class Meter {
    int count;

    public void Bump() {
        // The same alias one indirection further out: the monitor is process-wide and the local is
        // a name for it, not a new one.
        var gate = Shared.Gate;

        lock (gate) {
            count++;
        }
    }
}
