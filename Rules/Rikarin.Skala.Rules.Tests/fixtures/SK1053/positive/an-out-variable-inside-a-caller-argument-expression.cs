// #422: the whole call is the captured text, `out var parsed` included, so `out _` there would
// change what Probe.Run() returns.
using System.Runtime.CompilerServices;

public static class Probe {
    static string Check(bool condition, [CallerArgumentExpression("condition")] string text = "") => text;

    public static string Run() => Check(int.TryParse("12", out var parsed));
}
