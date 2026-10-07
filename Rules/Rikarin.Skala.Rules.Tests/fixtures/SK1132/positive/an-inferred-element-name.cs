// ⚠ Since C# 7.1 `(low, high)` gives the tuple the names `low` and `high`, and on the local's type they
// are indistinguishable from written ones — `IsExplicitlyNamedTupleElement` answers true. `range.low`
// binds, and is the better spelling for the same reason a written name is.
public static class Ranges {
    public static int Width(int low, int high) {
        var range = (low, high);
        return range.Item2 - range.Item1;
    }
}
