public sealed class Skipping {
    static int next;

    static bool Advance() => false;

    // With no incrementors, `continue` jumps to the condition in both forms — which is why the
    // rewrite is exact and not merely equivalent.
    public static int Sum() {
        var total = 0;
        for (; Advance();) {
            if (next < 0) {
                continue;
            }

            total += next;
        }

        return total;
    }
}
