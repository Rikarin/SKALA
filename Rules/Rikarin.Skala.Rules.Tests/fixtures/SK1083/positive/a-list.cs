using System.Collections.Generic;

public sealed class Registry {
    public static void Render(List<string> entries) {
        for (var i = 0; i < entries.Count; i++) {
            System.Console.WriteLine(entries[i]);
        }
    }
}
