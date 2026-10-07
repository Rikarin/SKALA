// #422: the condition's source text is what the caller-argument parameter receives, so rewriting it
// to `string.IsNullOrEmpty(name)` there would change what Probe.Run() returns. The finding stays; the
// host withholds the safe mark at this call site.
using System.Runtime.CompilerServices;

public static class Probe {
    static string Check(bool condition, [CallerArgumentExpression("condition")] string text = "") => text;

    public static string Run() {
        string? name = "";
        return Check(name == null || name.Length == 0);
    }
}
