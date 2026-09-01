using System;

public sealed class Counter {
    volatile int served;

    public Counter() {
        // ⚠ The constructor withdrawal must not reach through a lambda. This delegate is *written*
        // in the constructor and runs whenever somebody invokes it, on whatever thread they are on.
        OnServed = () => served++;
    }

    public Action OnServed { get; }

    public int Served => served;
}
