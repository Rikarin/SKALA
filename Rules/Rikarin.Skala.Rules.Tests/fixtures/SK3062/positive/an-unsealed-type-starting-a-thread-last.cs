using System.Threading;

// The other half of the sealed gate, and the reason it is `sealed` rather than "the start is last".
// `Pump` is not sealed, so `Derived`'s constructor runs after this one and keeps writing the object
// the worker thread is already reading — the start being the last statement of *this* constructor
// buys nothing at all.
class Pump {
    readonly int[] buffer;

    public Pump(int size) {
        buffer = new int[size];
        new Thread(Drain).Start();
    }

    void Drain() {
        _ = buffer.Length;
    }
}

sealed class Derived : Pump {
    readonly string name;

    public Derived(int size, string name) : base(size) {
        this.name = name;
    }

    public string Name => name;
}
