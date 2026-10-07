// #422 and #424: the argument's source text is captured by the callee, so rewriting `!(o is string)`
// there would change what Probe.Run() returns. The finding stands; the host marks its fix unsafe in
// the argument, and the runtime sweep pins that.
using System.Runtime.CompilerServices;

public static class Probe {
    static string Check(bool condition, [CallerArgumentExpression(nameof(condition))] string text = "") =>
        condition + " " + text;

    public static string Run() {
        object o = 1;
        return Check(!(o is string));
    }
}
