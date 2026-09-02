using System.Text.RegularExpressions;

// `?` admits one iteration, so there is no outer repetition to multiply the inner one by.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"(a+)?b").IsMatch(input);
}
