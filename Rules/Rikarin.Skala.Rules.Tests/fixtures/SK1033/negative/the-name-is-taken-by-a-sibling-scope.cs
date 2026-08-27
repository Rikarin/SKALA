using System.Collections.Generic;

// `count` would be lifted into the method's block, where the earlier `if` block's `count` is a
// nested scope: legal as two cousins, CS0136 once one of them moves up.
public sealed class Registry {
    readonly Dictionary<string, int> _counts = new();

    public int Read(string key, bool first) {
        if (first) {
            var count = 1;
            return count;
        }

        if (_counts.ContainsKey(key)) {
            var count = _counts[key];
            return count;
        }

        return 0;
    }
}
