using System.Collections.Concurrent;

// On a concurrent dictionary the ContainsKey/indexer pair is a race, not a redundancy. Rewriting it
// to TryGetValue changes the shape of the race rather than removing a double lookup, and that is a
// decision for a person.
public sealed class Registry {
    readonly ConcurrentDictionary<string, int> _counts = new();

    public int Read(string key) {
        if (_counts.ContainsKey(key)) {
            var count = _counts[key];
            return count;
        }

        return 0;
    }
}
