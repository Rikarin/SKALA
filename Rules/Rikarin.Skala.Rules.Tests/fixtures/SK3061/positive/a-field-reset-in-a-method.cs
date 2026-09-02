public sealed class Session {
    object gate = new object();

    int hits;

    public void Touch() {
        lock (gate) {
            hits++;
        }
    }

    public void Reset() {
        // ⚠ The second shape. A thread already inside `Touch` holds the old monitor; a thread that
        // arrives after this line takes the new one, and the two sit inside the same `lock` body at
        // the same time.
        gate = new object();
        hits = 0;
    }
}
