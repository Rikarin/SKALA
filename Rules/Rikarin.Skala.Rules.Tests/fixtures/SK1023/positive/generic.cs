class C<T> {
    readonly object gate = new();

    void M() { lock (gate) { } }
    void Other(C<int> other) { lock (other.gate) { } }
}
