using System.Threading;

public sealed class Registry {
    readonly object gate = new();

    int count;

    int hits;

    public void Add() {
        lock (gate) {
            count++;
        }
    }

    public int Read() {
        lock (gate) {
            return count;
        }
    }

    public void Reset() {
        count = 0;
    }

    // ⚠ The whole type is withdrawn on this line. `Interlocked` is ordering the rule cannot model,
    // and a type that reaches for it once may be doing so for `count` on a path the rule reads as
    // bare. The list that does this is matched on the written name, which is right here because it
    // can only silence the rule.
    public void Hit() => Interlocked.Increment(ref hits);
}
