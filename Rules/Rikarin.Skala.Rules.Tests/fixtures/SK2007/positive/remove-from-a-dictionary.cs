using System.Collections.Generic;
using System.Linq;

public sealed class CacheSweeper {
    public void Sweep(Dictionary<string, int> entries) {
        foreach (var entry in entries) {
            if (entry.Value == 0) {
                entries.Remove(entry.Key);
            }
        }
    }
}
