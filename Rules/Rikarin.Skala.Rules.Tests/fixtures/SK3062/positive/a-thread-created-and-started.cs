using System.Threading;

// Shape D through `Thread`. `Work` is a method group naming an instance method, so the thread holds
// `this`, and the `Start()` is in the same constructor — which is the whole difference between this
// and `negative/a-thread-never-started.cs`, where the same `new Thread(Work)` is harmless.
public sealed class Mill {
    readonly int[] grain;

    public Mill(int size) {
        new Thread(Work).Start();
        grain = new int[size];
    }

    public int Capacity => grain.Length;

    void Work() {
        for (var i = 0; i < grain.Length; i++) {
            grain[i] = i;
        }
    }
}
