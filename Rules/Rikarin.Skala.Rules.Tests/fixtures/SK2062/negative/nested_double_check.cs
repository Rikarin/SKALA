// Double-checked locking repeats the condition on purpose, and the two tests are nested rather
// than being rungs of one chain — the lock is exactly the thing that runs in between.
class C {
    static readonly object Gate = new();
    static string? value;

    static string Get() {
        if (value is null) {
            lock (Gate) {
                if (value is null) {
                    value = "loaded";
                }
            }
        }

        return value;
    }
}
