public sealed class Paths {
    // ⚠ The offset moves after the search arguments, so anything that could run is declined.
    public static bool HasSeparator(string path) => path.Substring(Start()).IndexOf('/') >= 0;

    static int Start() => 2;
}
