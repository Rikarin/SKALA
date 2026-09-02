using System.Text.RegularExpressions;

// `(?>…)` forbids backtracking into the group, which is the third way of writing the mitigation.
// The rule declines every `(?…)` construct it does not model rather than guessing at one.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"(?>a+)+b").IsMatch(input);
}
