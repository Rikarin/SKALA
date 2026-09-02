using System.Collections.Generic;

public sealed class Registry {
    // Any other reference to the receiver withdraws the finding: this one is exactly the mutation a
    // `foreach` would turn into an InvalidOperationException.
    public static void Grow(List<int> entries) {
        for (var i = 0; i < entries.Count; i++) {
            if (entries[i] > 0) {
                entries.Add(0);
            }
        }
    }
}
