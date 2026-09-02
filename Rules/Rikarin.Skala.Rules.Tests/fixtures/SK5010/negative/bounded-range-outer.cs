using System.Text.RegularExpressions;

// `{1,3}` has a ceiling, so the work is bounded by a constant rather than by the input.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"(a+){1,3}").IsMatch(input);
}
