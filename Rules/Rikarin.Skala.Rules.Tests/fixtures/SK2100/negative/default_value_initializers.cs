using System;

// ⚠ These assign exactly what every thread but the first already sees, so there is no divergence
// between threads to report. Firing here would report a declaration that is merely verbose.
static class Defaults {
    [ThreadStatic] static int count = 0;
    [ThreadStatic] static bool ready = false;
    [ThreadStatic] static string? name = null;
    [ThreadStatic] static double ratio = default;

    public static int Count => count;
    public static bool Ready => ready;
    public static string? Name => name;
    public static double Ratio => ratio;
}
