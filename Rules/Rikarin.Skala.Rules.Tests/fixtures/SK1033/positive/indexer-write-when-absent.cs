using System.Collections.Generic;

public sealed class Registry {
    readonly Dictionary<string, string> _names = new();

    public void Remember(string key, string name) {
        if (!_names.ContainsKey(key)) {
            _names[key] = name;
        }
    }
}
