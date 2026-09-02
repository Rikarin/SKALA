public sealed class Switcher {
    readonly object shared = new object();

    int hits;

    public void Touch(bool exclusive) {
        var gate = new object();
        if (exclusive) {
            // ⚠ The initializer is a creation, so the declarator test passes — and the local is a
            // shared object by the time the `lock` runs. "Never reassigned" is what stands between
            // this and a wrong finding, and it has to be checked over the whole function body
            // rather than at the declaration.
            gate = shared;
        }

        lock (gate) {
            hits++;
        }
    }
}
