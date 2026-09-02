using System.Text.RegularExpressions;

// The pattern does not compile as a regex at all. The scanner fails closed rather than reporting on
// something it could not read; whether this throws at run time is `SK2xxx`'s question, not this one.
public static class Validator {
    public static bool Looks(string input) => Regex.IsMatch(input, @"^(a+$");
}
