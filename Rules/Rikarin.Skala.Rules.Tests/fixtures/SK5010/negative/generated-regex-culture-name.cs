using System;
using System.Text.RegularExpressions;

// ⚠ Three arguments, and the third is a culture rather than a timeout — so counting arguments would
// call this bounded when it is not. It is a negative because `NonBacktracking` is set instead.
//
// ⚠ The implementation part throws because the fixture harness runs no source generators.
public static partial class Validator {
    [GeneratedRegex(@"^(a+)+$", RegexOptions.NonBacktracking | RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex Pattern();

    private static partial Regex Pattern() => throw new NotSupportedException("the generator writes this");

    public static bool Looks(string input) => Pattern().IsMatch(input);
}
