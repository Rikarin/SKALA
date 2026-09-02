public sealed class Channel {
    public object Gate = new object();

    int sent;

    public void Send() {
        // The same argument one accessibility further out, and the case where it is obviously
        // right: any assembly holding a `Channel` can assign `Gate`, so nothing this walk sees
        // bounds the set of writes.
        lock (Gate) {
            sent++;
        }
    }

    public void Reset() {
        Gate = new object();
        sent = 0;
    }
}
