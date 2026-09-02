namespace Fixtures.SK2241;

public static class UserMethodNamedPattern {
    static bool Accepts(string pattern) => pattern.Length > 0;

    // A user method with a `pattern` parameter is not `Regex`, and the containing-type check is the
    // only thing that says so.
    public static bool Check() => Accepts("(");
}
