// #422: the literal's spelling, escape and all, is the captured text, so `"A"` there would change
// what Probe.Run() returns although the string's value is the same.
using System.Runtime.CompilerServices;

public static class Probe {
    static string Show(string value, [CallerArgumentExpression("value")] string text = "") => value + " = " + text;

    public static string Run() => Show("\x41");
}
