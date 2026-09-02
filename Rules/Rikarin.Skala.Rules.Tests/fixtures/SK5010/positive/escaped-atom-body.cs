using System.Text.RegularExpressions;

// `\w` is one atom spelled with two characters, and the scanner has to read it as one.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"^(\w+)+$").IsMatch(input);
}
