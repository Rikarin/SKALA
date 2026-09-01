using System;
using System.Collections.Generic;

public sealed class Sorted {
    readonly Dictionary<string, int> inner = new();

    // Nothing says this projection is in the same order as the type's own enumeration.
    public IEnumerable<string> Keys {
        get {
            var keys = new List<string>(this.inner.Keys);
            keys.Sort();
            return keys;
        }
    }

    public int this[string key] => this.inner[key];
}

public sealed class Report {
    public static void Write(Sorted totals) {
        foreach (var key in totals.Keys) {
            Console.WriteLine(totals[key]);
        }
    }
}
