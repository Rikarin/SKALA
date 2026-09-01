public static class Counts {
    // `count.ToString()` is a conversion, and on an IFormattable it is not even the same
    // conversion an interpolation would perform.
    public static string Describe(int count) => "n=" + count.ToString();
}
