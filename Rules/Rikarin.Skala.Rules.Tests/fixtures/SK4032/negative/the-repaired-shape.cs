public sealed class Paths {
    public static bool HasSeparator(string path, int start) => path.IndexOf('/', start) >= 0;
}
