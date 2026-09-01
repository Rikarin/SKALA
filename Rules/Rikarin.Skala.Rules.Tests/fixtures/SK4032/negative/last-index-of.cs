public sealed class Paths {
    // ⚠ `LastIndexOf(value, startIndex)` searches backwards from that position — the opposite search.
    public static bool HasSeparator(string path, int start) => path.Substring(start).LastIndexOf('/') >= 0;
}
