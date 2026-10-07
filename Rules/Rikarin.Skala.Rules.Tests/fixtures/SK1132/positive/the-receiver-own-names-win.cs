// ⚠ One value, two views: `a` calls its elements `A`/`B` and `b` calls them `X`/`Y`. The rename must use
// the names of the receiver's own type at this site — `b.X`, not `b.A`, which does not compile.
public static class Views {
    public static int Read() {
        (int A, int B) a = (1, 2);
        (int X, int Y) b = a;
        return b.Item1 + a.Item2;
    }
}
