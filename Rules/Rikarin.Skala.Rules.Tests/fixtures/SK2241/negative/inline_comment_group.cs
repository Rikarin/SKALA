using System.Text.RegularExpressions;

namespace Fixtures.SK2241;

public static class InlineCommentGroup {
    // `(?#…)` is a comment group and parses; it is not an unbalanced parenthesis.
    public static readonly Regex Matcher = new(@"(?#a note)\d+");
}
