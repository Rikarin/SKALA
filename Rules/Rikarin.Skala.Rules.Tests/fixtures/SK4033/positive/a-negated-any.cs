using System.Collections.Concurrent;
using System.Linq;

public sealed class Cache {
    public static void Flush(ConcurrentDictionary<string, int> entries) {
        if (!entries.Keys.Any()) {
            return;
        }

        entries.Clear();
    }
}
