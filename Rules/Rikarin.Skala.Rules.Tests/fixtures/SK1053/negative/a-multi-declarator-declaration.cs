using System.Collections.Generic;

// One `=` per discard. Two declarators share a type and cannot both become `_`.
public sealed class Cache {
    readonly Dictionary<string, int> entries = new();

    public bool Drop(string first, string second) {
        bool one = entries.Remove(first), two = entries.Remove(second);
        return one && two;
    }
}
