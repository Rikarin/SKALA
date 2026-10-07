// ⚠ #423: `x * 0` on a `long` is a `long`, and the fix writes `0L` — `0` would turn `var r` into an
// `int`, which Probe would see.
public static class Probe {
    static object Zero(long x) {
        var r = x * 0;
        return r;
    }

    public static string Run() => Zero(7).GetType().Name;
}
