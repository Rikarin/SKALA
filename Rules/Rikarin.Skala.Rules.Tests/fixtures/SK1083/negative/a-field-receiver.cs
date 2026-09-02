using System.Collections.Generic;

public sealed class Registry {
    readonly List<int> entries = [];

    // A field is reachable by every call in the body, so "not mutated here" stops being readable.
    public int Total() {
        var total = 0;
        for (var i = 0; i < this.entries.Count; i++) {
            total += this.entries[i];
        }

        return total;
    }
}
