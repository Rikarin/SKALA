using System.Collections.Generic;

public sealed class Registry {
    readonly Dictionary<string, int> entries = new();

    public void Add(string key, int value) {
        // Locking on the collection being guarded is a design opinion, not a confusion of two
        // synchronization mechanisms. A `Dictionary` waits for nobody.
        lock (entries) {
            entries[key] = value;
        }
    }
}
