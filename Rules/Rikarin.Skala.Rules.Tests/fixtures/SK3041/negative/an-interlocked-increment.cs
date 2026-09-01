using System.Threading;

public sealed class Counter {
    int served;

    public void Serve() {
        // The repair the rule's message points at, written out. The field is deliberately not
        // `volatile`: `Interlocked` is already a full fence, and `ref` to a volatile field is CS0420.
        Interlocked.Increment(ref served);
    }

    public int Served => Volatile.Read(ref served);
}
