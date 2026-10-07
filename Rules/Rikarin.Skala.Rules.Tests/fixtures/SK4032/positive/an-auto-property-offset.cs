// An auto-property offset and a field of `this` as the search value run nothing, so the order they
// are evaluated in cannot be seen; the fix's result is pinned by Probe (#423).
public sealed class Scanner {
    readonly char separator = '/';

    public int Start { get; set; } = 2;

    public bool HasSeparator(string path) => path.Substring(Start).IndexOf(this.separator) >= 0;
}

public static class Probe {
    public static bool Run() => new Scanner().HasSeparator("a/b/c");
}
