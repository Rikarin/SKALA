// ⚠ #424: `b!` suppresses nothing — `b` is an `int` — but `F(a < b, c > (d))` without the `!` is a
// call to a generic method `a<b, c>` and no longer two comparisons (the #392 class). The only repair
// on offer breaks the build, so the rule declines.
public static class Probe {
    static string F(bool x, bool y) => x + " " + y;

    public static string Run() {
        int a = 1, b = 2, c = 3, d = 4;
        return F(a < b!, c > (d));
    }
}
