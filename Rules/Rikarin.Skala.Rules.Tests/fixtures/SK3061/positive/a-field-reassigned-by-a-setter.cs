public sealed class Generation {
    object gate = new();

    int epoch;

    public int Epoch {
        get => epoch;
        set {
            // The write is in a property setter rather than in a method, which is the same defect
            // and the one a "it is only ever assigned in the constructor, surely" reading by eye
            // misses. It is also the shape that proves the walk looks at accessors at all.
            epoch = value;
            gate = new object();
        }
    }

    public void Bump() {
        lock (this.gate) {
            epoch++;
        }
    }
}
