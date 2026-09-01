using System.Diagnostics.Contracts;

// The BCL's `[Pure]` promises no visible state change. A void method that makes no visible state
// change and returns nothing has no observable effect at all.
static class Validation {
    [Pure]
    public static void Check(string input) {
        _ = input.Length;
    }
}
