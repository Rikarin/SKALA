using System.Collections.Generic;

public sealed class Cache {
    public static void Fill(Dictionary<string, string> entries, string name, string first, string second) {
        entries[name] = first;
        entries[name] = second;
    }
}
