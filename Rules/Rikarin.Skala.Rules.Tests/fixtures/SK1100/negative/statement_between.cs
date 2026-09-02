public sealed class Tracing {
    static int Compute(int order) => order * 2;

    static void Log() { }

    // ⚠ The whole safety argument. Inlining across the `Log()` call would run `Compute` after it
    // instead of before it, and that is a different program whenever either can see the other.
    public static int Total(int order) {
        var result = Compute(order);
        Log();
        return result;
    }
}
