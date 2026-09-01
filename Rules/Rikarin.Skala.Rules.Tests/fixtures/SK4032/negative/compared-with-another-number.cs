public sealed class Paths {
    // `> 3` is a claim about *where*, and there the offset matters.
    public static bool Late(string path, int start) => path.Substring(start).IndexOf('/') > 3;
}
