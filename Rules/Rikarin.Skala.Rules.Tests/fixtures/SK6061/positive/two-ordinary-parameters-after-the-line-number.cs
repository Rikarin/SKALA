using System.Runtime.CompilerServices;

public static class Tracing {
    public static void Trace(
        [CallerLineNumber] int line = 0,
        string category = "general",
        bool verbose = false
    ) { }
}
