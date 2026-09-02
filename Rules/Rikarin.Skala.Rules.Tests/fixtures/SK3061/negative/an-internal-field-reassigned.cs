public sealed class Bus {
    internal object gate = new object();

    int sent;

    public void Send() {
        // ⚠ Everything shape 2 asks for except the accessibility: the field is reassigned in
        // `Reset` below. Declined anyway, because the claim shape 2 makes is that *every* write has
        // been seen, and that is only true while the compiler guarantees they are all inside this
        // type. An `internal` field can be assigned from anywhere in the assembly, so the walk here
        // is a sample rather than a census — and a rule that reports on a sample is guessing.
        lock (gate) {
            sent++;
        }
    }

    public void Reset() {
        gate = new object();
        sent = 0;
    }
}
