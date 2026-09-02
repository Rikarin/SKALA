using System.Text.RegularExpressions;

// The commonest real pattern with several groups in it, and none of them is repeated. Lookarounds
// are skipped rather than modelled.
public static class Password {
    public static bool Strong(string input) => Regex.IsMatch(input, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$");
}
