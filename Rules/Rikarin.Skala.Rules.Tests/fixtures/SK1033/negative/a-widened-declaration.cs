using System.Collections.Generic;

// `object count = _counts[key];` is legal and `out object count` is not: an out argument has to
// match the parameter type exactly.
public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    public object Read(string key) {
        if (_counts.ContainsKey(key)) {
            object count = _counts[key];
            return count;
        }

        return "none";
    }
}
