using System.Threading;

class C<T> {
    readonly object gate = new();

    void M() { lock (gate) { } }
    void Escape(C<int> other) { Monitor.Enter(other.gate); }
}
