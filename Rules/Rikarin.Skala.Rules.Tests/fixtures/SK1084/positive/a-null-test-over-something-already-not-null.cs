using System.Collections.Generic;
using System.Linq;

// ⚠ Anti-vacuity for `the-condition-narrows-a-nullable-the-body-uses`: the narrowing guard reads the
// compiler's flow state, not the syntax. `entry.Name` is not nullable, so `is not null` proves
// nothing the body did not already know and the rewrite is still offered.
public sealed class Entry {
    public string Name { get; init; } = "";
}

public sealed class Registry {
    public static void Render(IEnumerable<Entry> entries) {
        foreach (var entry in entries) {
            if (entry.Name is not null) {
                System.Console.WriteLine(entry.Name);
            }
        }
    }
}
