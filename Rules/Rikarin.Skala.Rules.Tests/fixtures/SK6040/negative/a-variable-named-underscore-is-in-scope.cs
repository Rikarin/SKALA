// ⚠ #424: `_` is a discard only where nothing called `_` is in scope; here `out _` would assign the
// parameter.
public static class Probe {
    static void Make(out int value) => value = 7;

    static int Use(int _) {
        Make(out int unused);
        return _;
    }

    public static int Run() => Use(1);
}
