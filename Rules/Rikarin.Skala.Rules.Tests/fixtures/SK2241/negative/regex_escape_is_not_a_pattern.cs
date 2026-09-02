using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class RegexEscapeIsNotAPattern {
    // ⚠ `Regex.Escape` takes `str`, not `pattern`. Its whole job is to accept text that is not a
    // pattern, so resolving the argument by parameter name rather than by position is what keeps this
    // quiet — a positional rule would report every call.
    public static string Quoted() => Regex.Escape("(");
}
