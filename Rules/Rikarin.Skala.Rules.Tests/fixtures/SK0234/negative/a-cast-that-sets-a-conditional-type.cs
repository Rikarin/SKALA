public static class Conditionals {
    // Removing the cast would make the conditional's type `int`, and `total` with it.
    public static long Pick(bool flag, int left, long right) {
        var total = flag ? (long)left : right;
        return total;
    }
}
