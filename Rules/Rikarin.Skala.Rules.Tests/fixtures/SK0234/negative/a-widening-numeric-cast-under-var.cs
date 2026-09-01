public static class Widening {
    // `var value = (long)1;` is a long and `var value = 1;` is an int. This is the shape Roslyn's
    // own IDE0004 gets wrong.
    public static long One() {
        var value = (long)1;
        return value;
    }
}
