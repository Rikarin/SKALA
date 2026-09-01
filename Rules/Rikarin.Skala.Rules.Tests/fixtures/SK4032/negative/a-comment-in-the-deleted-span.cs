public sealed class Paths {
    public static bool HasSeparator(string path, int start) =>
        path.Substring(/* skip the drive letter */ start).IndexOf('/') >= 0;
}
