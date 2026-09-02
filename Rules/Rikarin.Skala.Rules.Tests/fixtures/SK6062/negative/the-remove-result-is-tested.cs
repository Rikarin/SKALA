using System.Collections.Generic;

// `set.Remove(x)` as a statement puts nothing anywhere the program can observe. Read through the
// `bool` it is a use of the collection.
public static class Tested {
    public static bool Run(IEnumerable<string> items) {
        var seen = new HashSet<string>();
        var removed = false;

        foreach (var item in items) {
            seen.Add(item);
            if (seen.Remove(item)) {
                removed = true;
            }
        }

        return removed;
    }
}
