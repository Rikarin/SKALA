using System.Text.RegularExpressions;

// ⚠ This is the file that separates "the body contains a quantifier" from what the rule actually
// asks. Every iteration of `(abc*)+` has to start with `ab`, so the decomposition of any input is
// very nearly unique and there is nothing to backtrack through. The wider test would report it.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"(abc*)+").IsMatch(input);
}
