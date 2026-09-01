using System.Collections.Generic;

// `value` is read below the statement that writes it. An expression variable is scoped no wider
// than the statement that introduces it, so the later read may stop compiling.
public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public int Get(string key) {
        int value;
        entries.TryGetValue(key, out value);
        return value;
    }
}
