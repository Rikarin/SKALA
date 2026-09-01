using System.Collections.Generic;

// The `out` argument is inside the `if`'s block, so the inline declaration would be scoped there
// and the local was scoped to the method's block.
public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public void Touch(string key, bool enabled) {
        int value;
        if (enabled) {
            entries.TryGetValue(key, out value);
        }
    }
}
