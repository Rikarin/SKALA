using System.Collections.Generic;

// An `out` argument is not an assignment expression, so there is no node the rule visits — and the
// write is conditional here, which is what `TryGetValue` on a null map is supposed to mean.
public sealed class Lookup {
    public int Read(Dictionary<string, int>? map, string key) {
        var found = -1;
        map?.TryGetValue(key, out found);
        return found;
    }
}
