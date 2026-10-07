// A non-empty array and a list hold the same elements, in the same order, of the same type, and each
// creation is a fresh object. Pinned by Probe (#425). A list's capacity is not compared: it is the
// growth policy's, and the collection expression sizes the list to its count.
using System.Collections.Generic;

public static class Probe {
    static int[] Make() {
        int[] made = new int[] { 1, 2 };
        return made;
    }

    public static string Run() {
        List<string> names = new List<string> { "a", "b" };
        names.Add("c");
        var fresh = !ReferenceEquals(Make(), Make());
        return string.Join(",", names) + "/" + string.Join(",", Make()) + "/" + fresh + "/" + Make().GetType().Name;
    }
}
