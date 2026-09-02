using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class UnbalancedGroup {
    // Throws `RegexParseException` the first time this line runs.
    public static readonly Regex Matcher = new("(unclosed");
}
