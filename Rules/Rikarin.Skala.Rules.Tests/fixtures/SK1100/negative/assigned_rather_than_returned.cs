public sealed class Storing {
    static int Compute(int order) => order * 2;

    static int total;

    // The next statement uses the local and is not a `return` or a `throw`. Everything else is a
    // rewrite this rule does not write.
    public static void Record(int order) {
        var result = Compute(order);
        total = result;
    }
}
