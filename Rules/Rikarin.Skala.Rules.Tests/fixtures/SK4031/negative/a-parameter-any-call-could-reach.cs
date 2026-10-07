using System;
using System.Collections.Generic;

// A parameter may be the same dictionary some field holds, and `Log` may write it through that field.
// Nothing here proves otherwise, so the finding is withheld.
public sealed class Report {
    readonly Dictionary<string, int> _shared = new();

    void Log(string key) => _shared[key] = 0;

    public void Write(Dictionary<string, int> totals) {
        foreach (var key in totals.Keys) {
            Log(key);
            Console.WriteLine(key + ": " + totals[key]);
        }
    }

    public void WriteShared() => Write(_shared);
}
