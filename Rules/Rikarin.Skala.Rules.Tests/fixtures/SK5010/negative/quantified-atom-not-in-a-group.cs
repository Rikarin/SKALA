using System.Text.RegularExpressions;

// Two unbounded quantifiers in one pattern, neither nested inside the other. Sequential repetition
// is linear; it is nesting that multiplies.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"^\w+\s*\d+$").IsMatch(input);
}
