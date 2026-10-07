// ⚠ #425: a lambda that captured the receiver writes one of its members while an earlier argument
// runs. Measured for #412's audit: `R { A = 3, B = 7 }` before the fix and `R { A = 3, B = 2 }` after it.
using System;

public record struct R(int A, int B);

public static class Probe {
    public static string Run() {
        var x = new R(1, 2);
        Func<int> f = () => {
            x.B = 7;
            return 3;
        };
        var y = new R(f(), x.B);
        return y.ToString();
    }
}
