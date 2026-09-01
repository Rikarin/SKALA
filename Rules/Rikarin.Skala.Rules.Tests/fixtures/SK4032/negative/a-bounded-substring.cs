public sealed class Paths {
    // `Substring(n, length)` bounds the search; `IndexOf(x, n)` would look past the end of it.
    public static bool HasSeparator(string path, int start) => path.Substring(start, 5).IndexOf('/') >= 0;
}
