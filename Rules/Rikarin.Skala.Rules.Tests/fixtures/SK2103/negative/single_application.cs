using System.Diagnostics;

// The overwhelmingly common case: an AllowMultiple attribute applied exactly once.
static class Tracing {
    [Conditional("TRACE")]
    public static void Log(string message) {
        _ = message;
    }
}
