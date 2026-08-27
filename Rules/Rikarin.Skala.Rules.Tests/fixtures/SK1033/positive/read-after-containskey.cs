using System.Collections.Generic;

public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    public int Read(string key) {
        if (_counts.ContainsKey(key)) {
            var count = _counts[key];
            return count * 2;
        }

        return 0;
    }
}
