using System.Text.RegularExpressions;

namespace Corpus.Vulnerable;

/// <summary>SK5010 — nested unbounded quantifiers with nothing bounding the match.</summary>
public static class InputScreening {
    static readonly Regex Identifier = new(@"^([A-Za-z0-9_]+)+$");

    public static bool IsIdentifier(string input) => Identifier.IsMatch(input);

    public static bool IsQuoted(string input) => Regex.IsMatch(input, @"^""(\w+)+""$");
}
