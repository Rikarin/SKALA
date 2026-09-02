using System.Text.RegularExpressions;

// `NonBacktracking` selects an engine whose running time is linear whatever the pattern says, so
// there is nothing left to bound. The other mitigation, and equally not a finding.
public static class Validator {
    public static bool Looks(string input) => new Regex(@"^(a+)+$", RegexOptions.NonBacktracking).IsMatch(input);
}
