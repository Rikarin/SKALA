using System.Collections.Generic;

// The out variable would be in scope in the else branch and not definitely assigned there.
public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    public int Read(string key) {
        if (_counts.ContainsKey(key)) {
            var count = _counts[key];
            return count;
        } else {
            return -1;
        }
    }
}
