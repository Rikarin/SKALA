using System.Collections.Generic;

public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    public int Read(string key) {
        if (_counts.TryGetValue(key, out var count)) {
            return count;
        }

        return 0;
    }
}
