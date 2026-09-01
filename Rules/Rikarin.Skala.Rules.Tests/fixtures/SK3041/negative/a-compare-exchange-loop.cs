using System.Threading;

public sealed class Meter {
    int total;

    public void Record(int sample) {
        int before;
        int after;
        do {
            before = Volatile.Read(ref total);
            after = before + sample;
        } while (Interlocked.CompareExchange(ref total, after, before) != before);
    }

    public int Total => Volatile.Read(ref total);
}
