using System.Diagnostics;

// `Conditional` is AllowMultiple, so the compiler is silent. The second application says exactly
// what the first one said.
static class Tracing {
    [Conditional("TRACE")]
    [Conditional("TRACE")]
    public static void Log(string message) {
        _ = message;
    }
}
