using System.Collections.Generic;

// The key is a call. The original evaluates it twice and TryGetValue evaluates it once, so the
// rewrite is only sound where re-evaluation is free.
public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    static string Key(string raw) => raw.Trim();

    public int Read(string raw) {
        if (_counts.ContainsKey(Key(raw))) {
            var count = _counts[Key(raw)];
            return count;
        }

        return 0;
    }
}
