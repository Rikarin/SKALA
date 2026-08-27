using System.Collections.Concurrent;
using System.Linq;

public sealed class ConcurrentSweeper {
    // ⚠ A `ConcurrentDictionary` is *designed* to be written while it is enumerated: its enumerator
    // is a moving snapshot and there is no version counter to trip. This is why the rule matches a
    // closed list of BCL types and never `ICollection<T>`.
    public void Sweep(ConcurrentDictionary<string, int> entries) {
        foreach (var entry in entries) {
            if (entry.Value == 0) {
                entries.TryRemove(entry.Key, out _);
            }
        }
    }
}
