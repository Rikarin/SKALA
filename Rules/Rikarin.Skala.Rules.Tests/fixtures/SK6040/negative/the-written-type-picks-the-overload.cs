// ⚠ #424: the written type takes part in overload resolution. `Parse(1, out string text)` can only
// be the `long, out string` overload; `Parse(1, out _)` is the `int, out int` one, and it compiles
// (#412's audit).
public static class Probe {
    static string last = "";

    static void Parse(int a, out int value) {
        value = a;
        last = "Parse(int, out int)";
    }

    static void Parse(long a, out string value) {
        value = a.ToString();
        last = "Parse(long, out string)";
    }

    public static string Run() {
        Parse(1, out string text);
        return last;
    }
}
