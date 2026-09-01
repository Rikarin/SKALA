using System.Collections.Concurrent;

public sealed class Cache {
    public static void Flush(ConcurrentDictionary<string, int> entries) {
        if (entries.Count == 0) {
            return;
        }

        entries.Clear();
    }
}
