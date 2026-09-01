using System.Threading;

public sealed class Cache {
    readonly ReaderWriterLockSlim guard = new();

    int generation;

    public void Bump() {
        lock (guard) {
            generation++;
        }
    }
}
