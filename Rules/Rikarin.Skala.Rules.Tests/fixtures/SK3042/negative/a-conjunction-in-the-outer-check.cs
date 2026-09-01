public sealed class Cache {
    readonly object gate = new();

    string? loaded;

    bool enabled;

    public string? Load() {
        // ⚠ The outer condition is not the null check; it is the null check and something else.
        // What orders the read of `enabled` against the read of `loaded` is not decidable from
        // this shape, so the rule requires the whole condition and declines here.
        if (loaded == null && enabled) {
            lock (gate) {
                if (loaded == null) {
                    loaded = "value";
                }
            }
        }

        return loaded;
    }
}
