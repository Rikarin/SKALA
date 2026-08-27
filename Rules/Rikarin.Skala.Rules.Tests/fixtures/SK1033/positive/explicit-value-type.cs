using System.Collections.Generic;

public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    public int Read(string key) {
        if (_counts.ContainsKey(key)) {
            int count = _counts[key];
            return count;
        }

        return -1;
    }
}
