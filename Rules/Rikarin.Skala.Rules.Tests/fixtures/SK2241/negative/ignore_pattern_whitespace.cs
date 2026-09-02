using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class IgnorePatternWhitespace {
    // Under `IgnorePatternWhitespace` the spaces and the trailing comment are not part of the pattern.
    public static readonly Regex Matcher = new(@"\d+ # the count", RegexOptions.IgnorePatternWhitespace);
}
