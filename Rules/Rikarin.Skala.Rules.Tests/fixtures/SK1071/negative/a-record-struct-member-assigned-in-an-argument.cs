// ⚠ #425: an assignment to a member of the receiver is not an assignment to the receiver, and the
// write-scan saw only the second. Measured for #412's audit: `R { A = 5, B = 5 }` before the fix and
// `R { A = 5, B = 2 }` after it.
public record struct R(int A, int B);

public static class Probe {
    public static string Run() {
        var x = new R(1, 2);
        var y = new R(x.B = 5, x.B);
        return y.ToString();
    }
}
