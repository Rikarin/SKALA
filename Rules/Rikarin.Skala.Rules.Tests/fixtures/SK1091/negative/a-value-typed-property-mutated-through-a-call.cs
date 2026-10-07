// ⚠ #425: a value-typed property returns a copy, so `Tally.Bump()` mutates the copy and the write is
// lost; over the field the fix would write, it mutates the field. Measured for #412's audit: `0` before
// the fix and `2` after it.
public struct Counter {
    public int N;

    public void Bump() => N++;
}

public sealed class Holder {
    private Counter Tally { get; set; }

    public Holder() => Tally = new Counter();

    public void Hit() => Tally.Bump();

    public int Read() => Tally.N;
}

public static class Probe {
    public static int Run() {
        var holder = new Holder();
        holder.Hit();
        holder.Hit();
        return holder.Read();
    }
}
