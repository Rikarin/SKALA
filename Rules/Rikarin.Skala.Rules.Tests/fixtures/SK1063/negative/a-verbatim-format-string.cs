// A verbatim format string escapes its text differently, so the characters cannot be copied into a
// regular interpolated string unchanged.
public sealed class Windows {
    public string Line(string name) => string.Format(@"C:\logs\{0}", name);
}
