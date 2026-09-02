using System.Collections.Generic;
using System.Linq;

public sealed class Entry {
    public bool IsVisible { get; init; }
}

public sealed class Registry {
    public static void Render(IEnumerable<Entry> entries) {
        foreach (var entry in entries) {
            if (entry.IsVisible) {
                System.Console.WriteLine(entry);
            }
        }
    }
}
