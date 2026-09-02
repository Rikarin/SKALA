// The long-hand of the same idiom: the test is on the field being written, so the write happens
// once.
sealed class Registry {
    static Registry? shared;

    public Registry Ensure() {
        if (shared is null) {
            shared = this;
        }

        return shared;
    }
}
