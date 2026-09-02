using System;
using System.Text.RegularExpressions;

// The attribute's third parameter is `matchTimeoutMilliseconds` here and `cultureName` on another
// overload, so the rule tests the argument's type rather than counting the arguments.
//
// ⚠ The implementation part throws because the fixture harness runs no source generators; a stand-in
// that constructs nothing keeps the attribute the only thing this file could report.
public static partial class Validator {
    [GeneratedRegex(@"^(a+)+$", RegexOptions.None, 100)]
    private static partial Regex Pattern();

    private static partial Regex Pattern() => throw new NotSupportedException("the generator writes this");

    public static bool Looks(string input) => Pattern().IsMatch(input);
}
