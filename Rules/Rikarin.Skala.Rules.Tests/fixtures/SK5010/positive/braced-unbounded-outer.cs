using System.Text.RegularExpressions;

// `{2,}` is open-ended, so it is an unbounded quantifier written the long way.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"(x+){2,}").IsMatch(input);
}
