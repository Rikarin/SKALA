using System.Collections.Generic;

// The declaration is not the block's first statement, so the rewrite would move a read across a
// call that can change what it reads.
public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    public int Read(string key) {
        if (_counts.ContainsKey(key)) {
            Reset(key);
            var count = _counts[key];
            return count;
        }

        return 0;
    }

    void Reset(string key) => _counts[key] = 0;
}
