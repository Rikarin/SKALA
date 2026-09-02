public sealed class Totalling {
    // ⚠ A shared `return` hoists too. The statement lands directly after the `if`, which is exactly
    // where control arrived from either branch.
    public static int Total(bool retry, int seed) {
        var total = seed;
        if (retry) {
            total += 1;
            return total;
        } else {
            total += 2;
            return total;
        }
    }
}
