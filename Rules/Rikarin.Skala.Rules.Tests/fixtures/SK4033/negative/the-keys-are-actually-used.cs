using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

public sealed class Cache {
    // The snapshot is what the caller wants; that is what `Keys` is for.
    public static List<string> Names(ConcurrentDictionary<string, int> entries) => entries.Keys.ToList();

    public static void Write(ConcurrentDictionary<string, int> entries) {
        foreach (var key in entries.Keys) {
            Console.WriteLine(key);
        }
    }
}
