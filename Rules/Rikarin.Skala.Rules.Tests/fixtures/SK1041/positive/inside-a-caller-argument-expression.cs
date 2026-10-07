// #422: the lambda's source text is what the caller-argument parameter receives, so rewriting
// `total = total + 1` inside it would change what Probe.Run() returns. The host withholds the safe
// mark here.
using System;
using System.Runtime.CompilerServices;

public static class Probe {
    static string Check(Action action, [CallerArgumentExpression("action")] string text = "") => text;

    public static string Run() {
        var total = 0;
        return Check(() => { total = total + 1; });
    }
}
