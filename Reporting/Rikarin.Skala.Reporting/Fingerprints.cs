using System.Collections.Immutable;
using System.Globalization;
using System.IO.Hashing;
using System.Text;

namespace Rikarin.Skala.Reporting;

/// <summary>
///     The identity of a finding across edits to the file it sits in.
/// </summary>
/// <remarks>
///     docs/plan/09 § "The fingerprint". The property the whole baseline mechanism rests on is that a
///     finding survives the file being edited above it, reindented, or moved:
///     <code>skala/v2 = xxHash128( ruleId ⊕ normalizedSnippet ⊕ enclosingSymbolDisplayString ⊕ ordinalWithinSymbol )</code>
///     ⚠ <b>No line numbers, and no file path.</b> A fingerprint that moves when a line moves is a
///     baseline that expires every commit, and one that moves when a file is renamed is a baseline that
///     expires every refactor. The enclosing symbol carries the location information that is stable and
///     none of the information that is not.
///     <para>
///         ⚠ <see cref="Version1" /> is still emitted beside <see cref="Version2" />, and reading a baseline
///         falls back to it. M5 shipped v1 with the rule id, the normalised <em>message</em> and the file
///         name; adding the last two terms changes what the hash means, so it is a new version rather than
///         a silent redefinition — which is exactly what the version tag was put there for. A baseline
///         written by M5 keeps working, and the first <c>baseline update</c> after this change rewrites it
///         in v2.
///     </para>
/// </remarks>
public static class Fingerprints {
    const string DuplicatedBlockRuleId = "SK7020";
    const string DuplicatedBlockRelatedLocation = ", also at ";

    /// <summary>M5's fingerprint: rule id, normalised message, file name.</summary>
    public const string Version1 = "skala/v1";

    /// <summary>The full fingerprint of docs/plan/09 § "The fingerprint".</summary>
    public const string Version2 = "skala/v2";

    /// <summary>
    ///     Assigns <see cref="Finding.OrdinalWithinSymbol" /> across a whole run.
    /// </summary>
    /// <remarks>
    ///     ⚠ Deterministic by construction: the group key is everything the fingerprint uses <em>except</em>
    ///     the ordinal, and within a group the order is by path and then by offset. Two runs over the
    ///     same tree therefore number the same findings the same way, which is the only reason a
    ///     baseline written by one run is readable by the next.
    ///     <para>
    ///         ⚠ Called once, after merging and supersession, over the final set. Numbering before the
    ///         merge would number findings that are about to become one.
    ///     </para>
    /// </remarks>
    public static ImmutableArray<Finding> Assign(ImmutableArray<Finding> findings) {
        if (findings.IsEmpty) {
            return findings;
        }

        var order = findings
            .Select(static (finding, index) => (finding, index))
            .OrderBy(static entry => entry.finding.Path, StringComparer.Ordinal)
            .ThenBy(static entry => entry.finding.Start)
            .ThenBy(static entry => entry.finding.RuleId, StringComparer.Ordinal)
            .ToArray();

        var counters = new Dictionary<(string, string, string), int>();
        var assigned = new Finding[findings.Length];

        foreach (var (finding, index) in order) {
            var key = (finding.RuleId, finding.EnclosingSymbol, Identity(finding));
            counters.TryGetValue(key, out var ordinal);
            counters[key] = ordinal + 1;
            assigned[index] = finding with { OrdinalWithinSymbol = ordinal };
        }

        return [.. assigned];
    }

    /// <summary>Both fingerprint versions for one finding, for the SARIF's <c>partialFingerprints</c>.</summary>
    public static Dictionary<string, string> For(Finding finding) =>
        new(StringComparer.Ordinal) { [Version1] = V1(finding), [Version2] = V2(finding) };

    /// <summary>
    ///     ⚠ M5's fingerprint, unchanged, so that a baseline written before this milestone still reads.
    /// </summary>
    public static string V1(Finding finding) {
        var builder = new StringBuilder();
        builder.Append(finding.RuleId).Append(' ');
        Collapse(builder, finding.Message);
        builder.Append(' ').Append(Path.GetFileName(finding.Path));
        return Hash(builder);
    }

    /// <summary>The four-term fingerprint doc 09 specifies.</summary>
    public static string V2(Finding finding) =>
        V2(finding.RuleId, Identity(finding), finding.EnclosingSymbol, finding.OrdinalWithinSymbol);

    static string V2(string ruleId, string identity, string enclosingSymbol, int ordinalWithinSymbol) {
        var builder = new StringBuilder();
        builder.Append(ruleId).Append('');
        builder.Append(identity);
        builder.Append('').Append(enclosingSymbol).Append('');
        builder.Append(ordinalWithinSymbol.ToString(CultureInfo.InvariantCulture));
        return Hash(builder);
    }

    /// <summary>
    ///     Recomputes identities that can be recovered from an already-serialised SARIF result.
    /// </summary>
    /// <remarks>
    ///     ⚠ Existing SK7020 baselines contain the old, volatile v2 hash. Its stable identity can be
    ///     recovered from the message and the other two v2 terms that SARIF stores, allowing the old
    ///     entry to match without rewriting the baseline. Other rules return null because SARIF does
    ///     not store their source snippet.
    /// </remarks>
    internal static string? CanonicalStoredV2(
        string ruleId,
        string message,
        string enclosingSymbol,
        int ordinalWithinSymbol
    ) =>
        ruleId == DuplicatedBlockRuleId
            ? V2(ruleId, Normalize(MessageIdentity(ruleId, message)), enclosingSymbol, ordinalWithinSymbol)
            : null;

    /// <summary>
    ///     The text <see cref="V2" /> hashes to tell one finding from another: the snippet, or the
    ///     message when a rule reports without one.
    /// </summary>
    /// <remarks>
    ///     ⚠ It exists so that <see cref="Assign" /> and <see cref="V2" /> cannot disagree about what
    ///     makes two findings the same, and they did disagree: the counter keyed on <c>Snippet</c> while
    ///     the hash fell back to <c>Message</c>. Every rule that reports without a snippet — <c>SK7020</c>
    ///     is one — therefore had a group key whose third term was the empty string for all of its
    ///     findings, so the ordinal counted the rule's findings across the whole run instead of counting
    ///     repeats of one finding. One duplication appearing or disappearing above another then shifted
    ///     every later ordinal and rewrote every later fingerprint, which is a baseline that expires on
    ///     an unrelated edit.
    /// </remarks>
    static string Identity(Finding finding) =>
        Normalize(finding.Snippet.Length > 0 ? finding.Snippet : MessageIdentity(finding.RuleId, finding.Message));

    /// <summary>The stable text of a finding, before whitespace normalisation.</summary>
    /// <remarks>
    ///     ⚠ <c>SK7020</c>'s message names the other occurrences after <c>", also at "</c>. Those
    ///     locations are useful display text, but their paths and line ranges move independently of
    ///     the finding being fingerprinted. Hashing the suffix made inserting a line above either
    ///     occurrence — or renaming the paired file — invalidate the baseline.
    /// </remarks>
    static string MessageIdentity(string ruleId, string message) {
        if (ruleId == DuplicatedBlockRuleId) {
            var relatedLocation = message.IndexOf(DuplicatedBlockRelatedLocation, StringComparison.Ordinal);
            if (relatedLocation >= 0) {
                return message[..relatedLocation];
            }
        }

        return message;
    }

    /// <summary>
    ///     Whitespace collapsed, identifiers preserved (docs/plan/09).
    /// </summary>
    /// <remarks>
    ///     ⚠ Identifiers are kept deliberately, which is the difference between this and the
    ///     normalisation the duplication detector does. A fingerprint that ignored identifiers would
    ///     give the same identity to two different findings in two different methods, and a baseline
    ///     accepting one would accept the other.
    /// </remarks>
    public static string Normalize(string text) {
        var builder = new StringBuilder(text.Length);
        Collapse(builder, text);
        return builder.ToString();
    }

    static void Collapse(StringBuilder builder, string text) {
        var space = false;
        var started = false;
        foreach (var c in text) {
            if (c is ' ' or '\t' or '\r' or '\n') {
                space = started;
                continue;
            }

            if (space) {
                builder.Append(' ');
            }

            space = false;
            started = true;
            builder.Append(c);
        }
    }

    static string Hash(StringBuilder builder) =>
        Convert.ToHexStringLower(XxHash128.Hash(Encoding.UTF8.GetBytes(builder.ToString())));
}
