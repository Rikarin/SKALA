namespace Fixture.Own;

/// <summary>Somebody's own type that happens to carry the name. It is a plain object.</summary>
public sealed class SemaphoreSlim {
    public int Permits;
}

public sealed class Pool {
    readonly SemaphoreSlim slots = new();

    int taken;

    public void Take() {
        // The type is resolved, never matched on the written name.
        lock (slots) {
            taken++;
        }
    }
}
