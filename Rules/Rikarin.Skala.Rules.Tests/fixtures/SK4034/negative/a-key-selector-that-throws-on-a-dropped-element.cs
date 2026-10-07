using System.Collections.Generic;
using System.Linq;

// ⚠ #412's audit: the sort computes a key for every element, the null included, and throws. Filtered
// first, the null never reaches the key selector.
public static class Probe {
    public static string Run() {
        var names = new List<string?> { "bb", null, "a" };
        var sorted = names.OrderBy(name => name!.Length).Where(name => name != null).ToList();
        return string.Join(",", sorted);
    }
}
