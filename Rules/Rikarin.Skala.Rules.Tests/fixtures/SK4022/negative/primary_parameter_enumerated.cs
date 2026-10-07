// ⚠ `foreach` asks the capture for its enumerator, and a `GetEnumerator` that is not `readonly` runs
// on a copy under `readonly struct` (#412).
public struct Batch {
    public int Opened;

    public Batch GetEnumerator() {
        Opened++;
        return this;
    }

    public readonly bool MoveNext() => false;

    public readonly int Current => 0;
}

struct Drain(Batch batch) {
    public int Run() {
        var total = 0;
        foreach (var item in batch) {
            total += item;
        }

        return total;
    }

    public int Opened => batch.Opened;
}

public static class Probe {
    public static int Run() {
        var drain = new Drain(new Batch());
        drain.Run();
        return drain.Opened;
    }
}
