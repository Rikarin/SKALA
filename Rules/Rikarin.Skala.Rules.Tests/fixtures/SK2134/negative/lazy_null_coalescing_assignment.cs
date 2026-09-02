// ⚠ The look-alike. `??=` is instance code writing static state on purpose, and the guard is what
// makes it write-once: "the last one wins" is not true of it.
sealed class Registry {
    static Registry? shared;

    public Registry Ensure() {
        shared ??= this;
        return shared;
    }
}
