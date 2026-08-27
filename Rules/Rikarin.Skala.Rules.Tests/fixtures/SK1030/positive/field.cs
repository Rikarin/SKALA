using System.Collections.Generic;

public sealed class Cache {
    Dictionary<string, int>? _entries;

    public void Ensure() {
        _entries = _entries ?? new Dictionary<string, int>();
    }
}
