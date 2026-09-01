using System;
using System.Collections.Immutable;

public sealed class Report {
    public static void Write(ImmutableDictionary<int, string> names) {
        foreach (var id in names.Keys) {
            Console.WriteLine(names[id]);
        }
    }
}
