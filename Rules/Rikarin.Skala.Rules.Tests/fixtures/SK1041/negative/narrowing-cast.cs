public sealed class Truncating {
    long total;

    // ⚠ `total += 1` is NOT this program. The explicit `(int)` truncates to 32 bits and the result
    // widens back to long; the compound form supplies a conversion to `long`, not to `int`. A cast
    // on the right-hand side is never unwrapped, because proving it is exactly the narrowing the
    // compound form would supply is a different question for every pair of widths.
    public void Advance() {
        total = (int)(total + 1);
    }

    public long Value => total;
}
