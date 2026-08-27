using System.Collections.Generic;

// `TryAdd` is not a member of IDictionary<K, V>. It is an extension in CollectionExtensions that a
// project may not have in scope, so the rewrite is not available on the interface.
public sealed class Registry {
    readonly IDictionary<string, string> _names = new Dictionary<string, string>();

    public void Remember(string key, string name) {
        if (!_names.ContainsKey(key)) {
            _names[key] = name;
        }
    }
}
