using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    readonly List<string> entries = new();

    public IReadOnlyList<string> Items {
        get {
            var copy = entries.ToList();
            copy.Sort();
            return copy;
        }
    }
}
