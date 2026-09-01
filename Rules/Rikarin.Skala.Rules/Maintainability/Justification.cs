using System;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     The one test the <c>SK7050</c> family shares: is this text a reason, or a placeholder?
/// </summary>
/// <remarks>
///     ⚠ Deliberately a presence test and nothing more. <c>SK7050</c>, <c>SK7051</c>, <c>SK7070</c> and
///     <c>SK7071</c> all ask an author to write down why an exception exists, and none of them can read
///     what was written: the rule can prove that the field is blank or that it still says <c>TODO</c>,
///     and it can never prove that a paragraph of prose is true. Widening this predicate past the
///     placeholders is how a hygiene rule turns into a style opinion nobody asked for.
///     <para>
///         The list is <c>SK7051</c>'s, which is the wider of the two the shipped rules grew
///         independently. <c>SK7050</c> keeps its own narrower copy: changing what an id already
///         reports is a change to that rule, not a refactor, and ADR-012's promise is about meaning.
///     </para>
/// </remarks>
static class Justification {
    /// <summary>Whether the text reads as a reason rather than a blank or a placeholder.</summary>
    public static bool Meaningful(string text) {
        var value = text.Trim();
        return value.Length > 0
            && !value.StartsWith("TODO", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("FIXME", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("TBD", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("NONE", StringComparison.OrdinalIgnoreCase);
    }
}
