using System.Threading;

// ⚠ `new Thread(Work)` is not a race. A type that owns a worker routinely builds the thread in its
// constructor and starts it from a separate `Begin`, and nothing observes this object until somebody
// calls `Start`. The rule matches the `Start()` and then looks back for where that thread was made,
// so this shape is declined by construction rather than by a filter — and the same two lines,
// reordered into one constructor, are `positive/a-thread-created-and-started.cs`.
public sealed class Mill {
    readonly int[] grain;

    readonly Thread worker;

    public Mill(int size) {
        grain = new int[size];
        worker = new Thread(Work);
    }

    public void Begin() => worker.Start();

    void Work() {
        for (var i = 0; i < grain.Length; i++) {
            grain[i] = i;
        }
    }
}
