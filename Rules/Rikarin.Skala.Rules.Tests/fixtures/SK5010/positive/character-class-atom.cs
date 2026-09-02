using System.Text.RegularExpressions;

// The quantified atom inside the group is a character class rather than a single character.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"([A-Za-z]+)*!").IsMatch(input);
}
