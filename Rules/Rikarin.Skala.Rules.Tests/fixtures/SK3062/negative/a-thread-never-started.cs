using System.Threading;

// ⚠ `new Thread(Work)` is not a race. A type that owns a worker routinely builds and configures the
// thread in its constructor and starts it from a separate `Begin`, and nothing observes this object
// until somebody calls `Start`. The rule matches the `Start()` and then looks back for where that
// thread was made, so this shape is declined by shape D's own logic — the same two lines, reordered
// into one constructor, are `positive/a-thread-created-and-started.cs`.
//
// ⚠ The `TrySetApartmentState` call is load-bearing and was added after a sabotage run proved the
// fixture was worthless without it. With only `grain = …` and `worker = new Thread(Work)` in the
// body, the constructor holds no `this` expression, no `+=` and no invocation, so the cheap gate at
// the top of `Analyze` returned before shape D was ever consulted: breaking the `Start` requirement
// left this file green and it defended nothing. The invocation gets past the gate, which is what
// makes this a test of "the invocation must be `Thread.Start`" rather than of "there was nothing
// here to look at".
public sealed class Mill {
    readonly int[] grain;

    readonly Thread worker;

    public Mill(int size) {
        grain = new int[size];
        worker = new Thread(Work);
        worker.TrySetApartmentState(ApartmentState.MTA);
    }

    public void Begin() => worker.Start();

    void Work() {
        for (var i = 0; i < grain.Length; i++) {
            grain[i] = i;
        }
    }
}
