using System.Text.RegularExpressions;

// The textbook shape: thirty `a`s and a `b` take 2^29 decompositions to reject.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"^(a+)+$").IsMatch(input);
}
