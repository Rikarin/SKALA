using System.Text.RegularExpressions;

// The options are a combination rather than the single flag, so the rule has to test the bit rather
// than compare the value.
public static class Validator {
    public static bool Looks(string input) =>
        new Regex(@"^(a+)+$", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking).IsMatch(input);
}
