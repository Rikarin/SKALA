// ⚠ #425: `{ get; }` becomes a `readonly` field, which a constructor may still mutate in place — so a
// mutating call in the constructor is lost on the property's copy and kept on the field.
public struct Counter {
    public int N;

    public void Bump() => N++;
}

public sealed class Holder {
    private Counter Tally { get; }

    public Holder() {
        Tally = new Counter();
        Tally.Bump();
    }

    public int Read() => Tally.N;
}

public static class Probe {
    public static int Run() => new Holder().Read();
}
