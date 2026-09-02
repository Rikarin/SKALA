using System.Text.RegularExpressions;

// The attribute's third parameter is `matchTimeoutMilliseconds` here and `cultureName` on another
// overload, so the rule tests the argument's type rather than counting the arguments.
public static partial class Validator {
    [GeneratedRegex(@"^(a+)+$", RegexOptions.None, 100)]
    private static partial Regex Pattern();

    public static bool Looks(string input) => Pattern().IsMatch(input);
}
