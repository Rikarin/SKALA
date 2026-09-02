using System;
using System.Text.RegularExpressions;

// The modern spelling. `[GeneratedRegex]` has its own timeout parameter and this one omits it, so the
// same pattern is unbounded in exactly the same way.
//
// ⚠ The implementation part is written out, and throws, because the fixture harness compiles source
// without running source generators — an extended partial method with no implementation is CS8795.
// A stand-in that constructs nothing keeps the attribute the only thing this file can report.
public static partial class Validator {
    [GeneratedRegex(@"^(a+)+$")]
    private static partial Regex Pattern();

    private static partial Regex Pattern() => throw new NotSupportedException("the generator writes this");

    public static bool Looks(string input) => Pattern().IsMatch(input);
}
