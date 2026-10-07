// A faithful copy of a faithful copy: the array is the same elements, in the same order, of the same
// type, whichever copy it was taken from. Pinned by Probe (#425).
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    public static string Run() {
        IEnumerable<int> source = new[] { 3, 1, 2 }.Where(static n => n > 0);
        var array = source.ToList().ToArray();
        return string.Join(",", array) + "/" + array.GetType().Name;
    }
}
