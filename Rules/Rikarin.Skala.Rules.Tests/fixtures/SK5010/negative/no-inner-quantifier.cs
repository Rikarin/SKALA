using System.Text.RegularExpressions;

// A quantified group with a fixed-width body repeats linearly; there is no ambiguity to explore.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"(abc)+").IsMatch(input);
}
