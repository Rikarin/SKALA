using System;
using System.Text.RegularExpressions;

namespace Corpus.Safe;

/// <summary>
///     SK5010's twin: the same two screens, bounded the two ways a reviewer bounds them — a timeout on
///     the one that has to keep its lookahead-free pattern, and the linear engine on the other.
/// </summary>
public static class InputScreening {
    static readonly Regex Identifier =
        new(@"^([A-Za-z0-9_]+)+$", RegexOptions.None, TimeSpan.FromMilliseconds(100));

    public static bool IsIdentifier(string input) => Identifier.IsMatch(input);

    public static bool IsQuoted(string input) =>
        Regex.IsMatch(input, @"^""(\w+)+""$", RegexOptions.NonBacktracking);
}
