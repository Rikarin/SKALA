using System.Text.RegularExpressions;

// The atom under the outer quantifier is itself a group, which is still one atom.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"((ab)+)+").IsMatch(input);
}
