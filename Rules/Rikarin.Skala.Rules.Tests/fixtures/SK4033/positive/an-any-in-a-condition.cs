using System.Collections.Concurrent;
using System.Linq;

public sealed class Cache {
    public static void Flush(ConcurrentDictionary<string, int> entries) {
        while (entries.Values.Any()) {
            entries.Clear();
        }
    }
}
