using System.Threading;

public sealed class Counter {
    // `System.Threading.Lock` is a reference type; `readonly` is the right way to hold one and is
    // what SK1023 recommends.
    readonly Lock gate = new();

    int value;

    public void Increment() {
        lock (gate) {
            value++;
        }
    }
}
