// ⚠ #423: the explicit conversion to `int` is a method somebody wrote, and `(int)w * 0` → `0` would
// stop calling it.
public readonly struct Weight {
    public static int Conversions;

    public static explicit operator int(Weight w) {
        Conversions++;
        return 3;
    }
}

public static class Probe {
    public static int Run() {
        var w = new Weight();
        var r = (int)w * 0;
        return r + Weight.Conversions;
    }
}
