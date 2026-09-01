public sealed class Cache {
    string? loaded;

    public string Load() {
        // Lazy initialization with no synchronization whatsoever. Wrong for other reasons and not
        // double-checked locking, because there is no lock to be the second check.
        if (loaded == null) {
            if (loaded == null) {
                loaded = "value";
            }
        }

        return loaded;
    }
}
