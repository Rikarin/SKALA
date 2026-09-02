using System.Text.RegularExpressions;

// ⚠ The pattern is the positive fixture's and is provably dangerous — but whether these options
// carry `NonBacktracking` is unknowable here, so reporting would be reporting over the mitigation.
// Newtonsoft.Json's `Regex.IsMatch(input, patternText, GetRegexOptions(optionsText))` is this shape.
public static class Validator {
    public static bool Looks(string input, RegexOptions options) => Regex.IsMatch(input, @"^(a+)+$", options);
}
