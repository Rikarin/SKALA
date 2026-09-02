using System.Text.RegularExpressions;

// The modern spelling. `[GeneratedRegex]` has its own timeout parameter and this one omits it, so
// the same pattern is unbounded in exactly the same way.
public static partial class Validator {
    [GeneratedRegex(@"^(a+)+$")]
    private static partial Regex Pattern();

    public static bool Looks(string input) => Pattern().IsMatch(input);
}
