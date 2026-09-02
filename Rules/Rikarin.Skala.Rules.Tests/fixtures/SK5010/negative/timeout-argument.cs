using System;
using System.Text.RegularExpressions;

// The pattern is exactly the positive fixture's. The timeout is the whole difference, and it is the
// mitigation this rule recommends — reporting it would be reporting the fix.
public static class Validator {
    public static bool Looks(string input) =>
        new Regex(@"^(a+)+$", RegexOptions.None, TimeSpan.FromMilliseconds(100)).IsMatch(input);
}
