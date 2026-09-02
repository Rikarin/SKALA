using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class EscapedParenthesis {
    // The parenthesis is escaped, so the pattern parses. A rule that searched for "(" would report it.
    public static readonly Regex Matcher = new(@"\(unclosed");
}
