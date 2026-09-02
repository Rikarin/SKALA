using System.Text.RegularExpressions;

// ⚠ Three arguments and the third is a culture, not a timeout — so this one IS unbounded. It is a
// negative only because `NonBacktracking` is set; without that flag the rule must still report it,
// which is what stops "three arguments means a timeout" from being the test.
public static partial class Validator {
    [GeneratedRegex(@"^(a+)+$", RegexOptions.NonBacktracking | RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex Pattern();

    public static bool Looks(string input) => Pattern().IsMatch(input);
}
