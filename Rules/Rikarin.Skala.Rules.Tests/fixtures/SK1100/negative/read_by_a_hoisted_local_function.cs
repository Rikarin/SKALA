public sealed class Hoisted {
    static int Compute(int order) => order * 2;

    // ⚠ Why the reference count is taken over the whole member rather than the enclosing block. The
    // local function is written below the `return` and captures the local from above its own
    // declaration, so a count that stopped at the block would delete a declaration still in use.
    public static int Total(int order) {
        var result = Compute(order);
        return result;

        int Twice() => result * 2;
    }
}
