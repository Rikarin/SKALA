// ⚠ #425: the call reads `x.B` after `x.Bump()` has run; `x with { A = x.Bump() }` clones `x` first.
// Measured for #412's audit: `R { A = 9, B = 100 }` before the fix and `R { A = 9, B = 2 }` after it.
public record struct R(int A, int B) {
    public int Bump() {
        B = 100;
        return 9;
    }
}

public static class Probe {
    public static string Run() {
        var x = new R(1, 2);
        var y = new R(x.Bump(), x.B);
        return y.ToString();
    }
}
