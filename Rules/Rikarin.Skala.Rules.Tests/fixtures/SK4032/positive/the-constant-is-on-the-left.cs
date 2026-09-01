public sealed class Paths {
    public static bool Missing(string path, int start) => -1 == path.Substring(start).IndexOf('/');
}
