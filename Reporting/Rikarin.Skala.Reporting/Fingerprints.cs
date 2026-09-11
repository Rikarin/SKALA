using Rikarin.Skala.Rules.Metadata;
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
///     <code>
/// skala/v3 = xxHash128( ruleId ⊕ normalizedSnippet ⊕ enclosingSymbolDisplayString
///                       ⊕ fileNameWhenNoSymbol ⊕ ordinalWithinScope )
///     </code>
///     ⚠ <b>No line numbers, and no file path.</b> A fingerprint that moves when a line moves is a
///     baseline that expires every commit, and one that moves when a file is renamed is a baseline that
///     expires every refactor. The enclosing symbol carries the location information that is stable and
///     none of the information that is not.
///     <para>
///         ⚠ <b>A finding with no enclosing symbol is scoped to its file name instead</b> (#365). v2 gave
///         such a finding no location term at all, so "the ordinal within the symbol" was the ordinal
///         within <em>nothing</em> — a counter over every finding of that rule with that text in the
///         whole run, numbered in path order. One new over-long line in
///         <c>Analysis/…/CrashedRunCacheTests.cs</c> then took ordinal 15 from
///         <c>Tools/…/McpServerTests.cs:249</c>, a file the commit never touched, and the self-gate
///         blamed the wrong file while silently accepting the new one under the old entry. The file
///         <em>name</em> is the same compromise <see cref="Version1" /> makes: it survives a directory
///         move, which the enclosing symbol also survives, and not a rename, which for a finding with no
///         symbol there is nothing else to anchor to. Measured on this repository's own baseline before
///         the change: 371 of 1 093 entries were symbol-less — 292 <c>SK0002</c>, 77 <c>SK7020</c>, two
///         <c>SK0003</c> — and 189 of the 292 held an ordinal that only meant something relative to
///         other files.
///     </para>
///     <para>
///         ⚠ Every version is still readable, and the fallback is one-directional. <see cref="Version2" />
///         is no longer written — a v2 hash computed from a scoped ordinal would carry a second meaning
///         under the same key, which is the thing the version tag exists to prevent — but a baseline
///         holding v2-only entries is matched by recomputing v2 the way M6 did, run-wide ordinal and
///         all (<see cref="LegacyV2" />), until the first <c>baseline update</c> rewrites it. A newer
///         hash never matches an older-only entry the other way round, because each older version is
///         the weaker identity and letting it match would silently widen what the baseline suppresses.
///     </para>
/// </remarks>
public static class Fingerprints {
    const string DuplicatedBlockRelatedLocation = ", also at ";
    const char Separator = '\u0001';

    /// <summary>M5's fingerprint: rule id, normalised message, file name.</summary>
    public const string Version1 = "skala/v1";

    /// <summary>
    ///     M6's fingerprint: rule id, identity, enclosing symbol, and an ordinal counted across the run.
    ///     Read for baselines written before <see cref="Version3" />; never written.
    /// </summary>
    public const string Version2 = "skala/v2";

    /// <summary>The fingerprint of docs/plan/09 § "The fingerprint", with the ordinal scoped to the file.</summary>
    public const string Version3 = "skala/v3";

    /// <summary>
    ///     Assigns <see cref="Finding.OrdinalWithinSymbol" /> across a whole run.
    /// </summary>
    /// <remarks>
    ///     ⚠ Deterministic by construction: the group key is everything the fingerprint uses <em>except</em>
    ///     the ordinal, and within a group the order is by path and then by offset. Two runs over the
    ///     same tree therefore number the same findings the same way, which is the only reason a
    ///     baseline written by one run is readable by the next. The ordinal is a position in that order,
    ///     not an offset: inserting a line above two identical findings moves both offsets and neither
    ///     ordinal.
    ///     <para>
    ///         ⚠ "Everything the fingerprint uses" includes the file-name term, so a symbol-less
    ///         finding's group never reaches outside its file. Key the counter on fewer terms than the
    ///         hash and the ordinal distinguishes findings the hash then cannot tell apart from each
    ///         other's neighbours in other files — which is #365.
    ///     </para>
    ///     <para>
    ///         ⚠ Called once, after merging and supersession, over the final set. Numbering before the
    ///         merge would number findings that are about to become one.
    ///     </para>
    /// </remarks>
    public static ImmutableArray<Finding> Assign(ImmutableArray<Finding> findings) {
        if (findings.IsEmpty) {
            return findings;
        }

        var counters = new Dictionary<(string, string, string, string), int>();
        var assigned = new Finding[findings.Length];

        foreach (var (finding, index) in Ordered(findings)) {
            var key = (finding.RuleId, finding.EnclosingSymbol, FileScope(finding), Identity(finding));
            counters.TryGetValue(key, out var ordinal);
            counters[key] = ordinal + 1;
            assigned[index] = finding with { OrdinalWithinSymbol = ordinal };
        }

        return [.. assigned];
    }

    /// <summary>The fingerprint versions written for one finding, for the SARIF's <c>partialFingerprints</c>.</summary>
    public static Dictionary<string, string> For(Finding finding) =>
        new(StringComparer.Ordinal) { [Version1] = V1(finding), [Version3] = V3(finding) };

    /// <summary>
    ///     ⚠ M5's fingerprint, unchanged, so that a baseline written before M6 still reads.
    /// </summary>
    public static string V1(Finding finding) {
        var builder = new StringBuilder();
        builder.Append(finding.RuleId).Append(' ');
        Collapse(builder, finding.Message);
        builder.Append(' ').Append(Path.GetFileName(finding.Path));
        return Hash(builder);
    }

    /// <summary>The fingerprint doc 09 specifies, over the ordinal <see cref="Assign" /> gave the finding.</summary>
    public static string V3(Finding finding) =>
        V3(finding.RuleId, Identity(finding), finding.EnclosingSymbol, FileScope(finding), finding.OrdinalWithinSymbol);

    static string V3(string ruleId, string identity, string enclosingSymbol, string fileScope, int ordinal) {
        var builder = new StringBuilder();
        builder.Append(ruleId).Append(Separator);
        builder.Append(identity).Append(Separator);
        builder.Append(enclosingSymbol).Append(Separator);
        builder.Append(fileScope).Append(Separator);
        builder.Append(ordinal.ToString(CultureInfo.InvariantCulture));
        return Hash(builder);
    }

    /// <summary>
    ///     ⚠ M6's <see cref="Version2" /> for every finding of a run, computed the way M6 computed it.
    /// </summary>
    /// <remarks>
    ///     The v2 ordinal was counted over the whole run — the defect of #365 — so a single finding's v2
    ///     is not a function of that finding: it depends on how many identical symbol-less findings sit
    ///     in files that sort before it. This recomputes M6's numbering over the run being compared so
    ///     that a baseline written by M6 matches exactly what M6 would have matched, defect included,
    ///     until <c>baseline update</c> replaces it. The result is aligned with <paramref name="findings" />.
    ///     ⚠ Its only caller is the legacy path of <see cref="Baseline" />; nothing writes what it returns.
    /// </remarks>
    internal static ImmutableArray<string> LegacyV2(ImmutableArray<Finding> findings) {
        if (findings.IsEmpty) {
            return [];
        }

        var counters = new Dictionary<(string, string, string), int>();
        var hashes = new string[findings.Length];

        foreach (var (finding, index) in Ordered(findings)) {
            var identity = Identity(finding);
            var key = (finding.RuleId, finding.EnclosingSymbol, identity);
            counters.TryGetValue(key, out var ordinal);
            counters[key] = ordinal + 1;
            hashes[index] = LegacyV2(finding.RuleId, identity, finding.EnclosingSymbol, ordinal);
        }

        return [.. hashes];
    }

    static string LegacyV2(string ruleId, string identity, string enclosingSymbol, int ordinalWithinSymbol) {
        var builder = new StringBuilder();
        builder.Append(ruleId).Append(Separator);
        builder.Append(identity);
        builder.Append(Separator).Append(enclosingSymbol).Append(Separator);
        builder.Append(ordinalWithinSymbol.ToString(CultureInfo.InvariantCulture));
        return Hash(builder);
    }

    /// <summary>
    ///     Recomputes M6 identities that can be recovered from an already-serialised SARIF result.
    /// </summary>
    /// <remarks>
    ///     ⚠ SK7020 baselines written before its identity was corrected contain a volatile v2 hash. What
    ///     M6 <em>should</em> have stored can be recovered from the message and the other two v2 terms
    ///     that SARIF stores, which is how <see cref="Baseline" /> tells such an entry from one whose v2
    ///     is merely old. Other rules return null because SARIF does not store their source snippet.
    /// </remarks>
    internal static string? CanonicalStoredV2(
        string ruleId,
        string message,
        string enclosingSymbol,
        int ordinalWithinSymbol
    ) =>
        ruleId == RuleIds.DuplicatedBlock
            ? LegacyV2(ruleId, Normalize(MessageIdentity(ruleId, message)), enclosingSymbol, ordinalWithinSymbol)
            : null;

    static IEnumerable<(Finding finding, int index)> Ordered(ImmutableArray<Finding> findings) =>
        findings
            .Select(static (finding, index) => (finding, index))
            .OrderBy(static entry => entry.finding.Path, StringComparer.Ordinal)
            .ThenBy(static entry => entry.finding.Start)
            .ThenBy(static entry => entry.finding.RuleId, StringComparer.Ordinal);

    /// <summary>
    ///     The fingerprint's location term when there is no enclosing symbol: the file name, or empty
    ///     when the symbol is present and already carries the location.
    /// </summary>
    static string FileScope(Finding finding) =>
        finding.EnclosingSymbol.Length > 0 ? string.Empty : Path.GetFileName(finding.Path);

    /// <summary>
    ///     The text <see cref="V3" /> hashes to tell one finding from another: the snippet, or the
    ///     message when a rule reports without one.
    /// </summary>
    /// <remarks>
    ///     ⚠ It exists so that <see cref="Assign" /> and <see cref="V3" /> cannot disagree about what
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
        if (ruleId == RuleIds.DuplicatedBlock) {
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
