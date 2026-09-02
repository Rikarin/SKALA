using System.Text.RegularExpressions;

// The static overloads carry the pattern at the call, so the call is where the fact lives.
public static class Validator {
    public static bool Looks(string input) => Regex.IsMatch(input, @"^([a-z]*)*$");
}
