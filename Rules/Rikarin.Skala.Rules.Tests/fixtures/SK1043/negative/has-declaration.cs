public sealed class Declared {
    // Converting this would have to lift `i` into the enclosing scope. That is a different
    // rewrite with a different risk, and it is not this one.
    public static int Walk(int limit) {
        for (int i = 0; i < limit;) {
            i += 2;
        }

        return limit;
    }
}
