// The inner test is the one the guard walk finds, which is what makes double-checked locking fall
// on the declined side rather than needing a rule of its own.
sealed class Registry {
    static readonly object Gate = new();
    static Registry? shared;

    public Registry Ensure() {
        if (shared is null) {
            lock (Gate) {
                if (shared is null) {
                    shared = this;
                }
            }
        }

        return shared;
    }
}
