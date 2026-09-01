using System.Threading;

public sealed class Counter {
    readonly Lock gate = new();

    int value;

    public void Increment() {
        // `System.Threading.Lock` is a synchronization primitive and is also the type a C# 13
        // `lock` statement is meant to be taken over — the compiler lowers this to
        // `Lock.EnterScope`. Reporting it would contradict `SK1023`'s own fix.
        lock (gate) {
            value++;
        }
    }
}
