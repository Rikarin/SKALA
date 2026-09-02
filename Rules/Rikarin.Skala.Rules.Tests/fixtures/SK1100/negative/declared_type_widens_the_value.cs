public sealed class Boxing {
    // ⚠ The conversion hazard, in the smallest program that shows it. The declaration widens `1`
    // from `int` to `long`, so the returned box holds a `long`; `return 1;` would box an `int`.
    // Deleting the declaration would move a conversion the `return` never performed.
    public static object Widened() {
        long value = 1;
        return value;
    }
}
