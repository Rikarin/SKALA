using System.Diagnostics;

public static class Guard {
    // `Debug.Assert(bool, ref AssertInterpolatedStringHandler)` — the handler is what keeps the
    // message from being built when the assertion holds, so the `$` is what selects it.
    public static void Check(double bleed) =>
        Debug.Assert(bleed > 0, $"the bleed is {bleed:F1} kg/s " + $"which cannot be negative");
}
