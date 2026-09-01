using System;
using System.Collections.Generic;

public static class Conditional {
    public static void Trace(Dictionary<string, int> lookup, string key) {
        lookup.TryGetValue(key, out var slot);
#if DEBUG
        Console.WriteLine(slot);
#endif
    }
}
