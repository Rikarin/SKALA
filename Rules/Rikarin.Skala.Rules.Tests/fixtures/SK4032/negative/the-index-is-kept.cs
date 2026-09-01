public sealed class Paths {
    // ⚠ The two spellings differ by exactly `start`, so the number itself is not interchangeable.
    public static int Separator(string path, int start) => path.Substring(start).IndexOf('/');
}
