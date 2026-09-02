using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class IgnorePatternWhitespace {
    // ⚠ This pattern is genuinely options-dependent, and that is the whole point of the fixture.
    // Under `None` the `(` opens a group nothing closes and `Regex` throws "Not enough )'s"; under
    // `IgnorePatternWhitespace` everything after the `#` is a comment and the pattern is fine. A rule
    // that read the pattern without reading the options would report this.
    public static readonly Regex Matcher = new(@"\d+ # (unclosed comment", RegexOptions.IgnorePatternWhitespace);
}
