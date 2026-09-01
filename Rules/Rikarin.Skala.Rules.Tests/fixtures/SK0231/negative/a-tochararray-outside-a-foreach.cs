public static class Copies {
    // The array is a `char[]` the caller may index, sort or hand on. A string is none of those.
    public static char[] Chars(string line) => line.ToCharArray();
}
