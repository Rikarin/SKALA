public sealed class Paths {
    static readonly char[] Separators = ['/', '\\'];

    public static bool HasSeparator(string path) => path.Substring(2).IndexOfAny(Separators) < 0;
}
