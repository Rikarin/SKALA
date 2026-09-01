public sealed class Paths {
    public static bool HasSeparator(string path, int start) => path.Substring(start).IndexOf('/') >= 0;
}
