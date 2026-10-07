// #422: the condition's source text is what the caller-argument parameter receives, so cancelling
// the double negation there would change what Probe.Run() returns.
using System.Runtime.CompilerServices;

public static class Probe {
    static string Check(bool condition, [CallerArgumentExpression("condition")] string text = "") => text;

    public static string Run() {
        var count = 5;
        return Check(count is not not 5);
    }
}
