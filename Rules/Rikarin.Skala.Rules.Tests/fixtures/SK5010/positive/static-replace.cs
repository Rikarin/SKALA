using System.Text.RegularExpressions;

// `Replace` backtracks exactly as `IsMatch` does; the rule finds the pattern by parameter name
// rather than by listing the methods, so a sixth static overload arrives covered.
public static class Scrubber {
    public static string Digits(string input) => Regex.Replace(input, @"(\d+)+", "#");
}
