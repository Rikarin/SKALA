using System.Text.RegularExpressions;

// Whether an unknown pattern backtracks is a question about another method. Silence, for the same
// reason SK5009 says nothing about a resolver read out of a variable.
public static class Validator {
    public static bool Looks(string input, string pattern) => Regex.IsMatch(input, pattern);
}
