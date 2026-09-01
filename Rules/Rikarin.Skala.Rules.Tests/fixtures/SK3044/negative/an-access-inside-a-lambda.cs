using System;

public sealed class Registry {
    readonly object gate = new();

    int count;

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

    // ⚠ Withdraws the field, not this one access. The delegate runs whenever somebody invokes it,
    // holding whatever they happen to hold, so neither "guarded" nor "unguarded" is knowable — and
    // the wrong answer here is a finding rather than a silence.
    public Action Resetter => () => count = 0;

    public void Reset() => count = 0;
}
