using System.Collections.Generic;

public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    public int Read(string key, string other) {
        if (_counts.ContainsKey(key)) {
            var count = _counts[other];
            return count;
        }

        return 0;
    }
}
