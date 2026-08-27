using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Rules.Metadata;

/// <summary>
/// What fraction of the rule catalogue is built, computed from the catalogue and the registry
/// rather than counted by hand.
/// </summary>
/// <remarks>
/// <para>
/// ⚠ <b>The number this replaces went stale inside one merge.</b> Doc 08's status table recorded
/// "21 shipped, 19.8 %", measured at <c>8cbd66d</c>; M8's five <c>SK5xxx</c> landed after the table
/// was typed and nothing noticed. A catalogue that misreports its own coverage is the same failure
/// as a document describing behaviour the tool does not have — so the count is generated into the
/// document between markers, and <c>RuleCatalogTests.TheCoverageBlock_MatchesTheRegistry</c> fails
/// when the two disagree.
/// </para>
/// <para>
/// ⚠ <b>Three states, not two.</b> Shipped / cut-with-reason / outstanding is what makes the number
/// honest. Generating only "shipped versus named" would count a rule that was deliberately disposed
/// of as a rule the project is failing to build, and doc 08's whole § "Reasons that justify a cut"
/// exists to keep those apart. Twelve rules were declared cut in M7's retrospective with no reason
/// recorded anywhere; the document reclassifies them as outstanding, and so does this.
/// </para>
/// <para>
/// ⚠ It reads the document rather than a second list. A coverage report maintained beside the
/// catalogue is two registers, and two registers disagree — which is the defect being fixed, not a
/// different one.
/// </para>
/// </remarks>
public static class RuleCoverage {
    public const string BeginMarker = "<!-- BEGIN GENERATED COVERAGE -->";
    public const string EndMarker = "<!-- END GENERATED COVERAGE -->";

    /// <summary>
    /// ⚠ The two boundaries that are not band edges. Doc 08 § "The ranges" splits the async band at
    /// <c>SK3499</c>/<c>SK3500</c>, so neither is a rule even though neither ends in 000 or 999.
    /// </summary>
    static readonly string[] RangeBoundaries = ["SK3499", "SK3500"];

    /// <summary>
    /// ⚠ A band edge — <c>SK1000</c>–<c>SK1999</c> and the eight like it — names a range, not a
    /// rule.
    /// </summary>
    /// <remarks>
    /// This is worth fifteen ids and it is the difference between a coverage figure of 23.2 % and
    /// one of 26.1 %. Counting the edges puts fifteen rules on the backlog that were never planned
    /// and can never be built, which understates the project against a denominator it invented —
    /// exactly the kind of number this whole block exists to stop being wrong.
    /// </remarks>
    static bool IsBandEdge(string id) =>
        id.EndsWith("000", StringComparison.Ordinal) || id.EndsWith("999", StringComparison.Ordinal);

    static readonly Regex RuleId = new(@"\bSK\d{4}\b", RegexOptions.Compiled);

    /// <summary>A rule the catalogue names, and what became of it.</summary>
    public enum State {
        /// <summary>Present in <c>rules.json</c>. It exists and runs.</summary>
        Shipped,

        /// <summary>
        /// Deliberately not built, with a reason recorded in § "Cut, with the reason".
        /// </summary>
        Cut,

        /// <summary>
        /// ⚠ Allocated, superseded by a live id, and never to be built. Distinct from
        /// <see cref="Cut"/> because nothing was decided against the *rule* — the id was a
        /// duplicate. Counting it as outstanding would put work on the roadmap that must never
        /// happen; counting it as cut would file a clerical error under "decisions".
        /// </summary>
        Retired,

        /// <summary>Named, not built, not disposed of. The backlog.</summary>
        Outstanding
    }

    public sealed class Result {
        public IReadOnlyDictionary<string, State> States { get; init; } = new Dictionary<string, State>();

        public int Named => States.Count;

        public int Count(State state) => States.Values.Count(value => value.Equals(state));

        /// <summary>
        /// ⚠ Shipped over named, with retired ids excluded from the denominator.
        /// </summary>
        /// <remarks>
        /// A retired id is not a thing the project could ship, so leaving it in the denominator
        /// would make the percentage permanently unreachable by exactly the number of clerical
        /// duplicates ever made.
        /// </remarks>
        public double Percentage =>
            Named - Count(State.Retired) == 0
                ? 0
                : 100.0 * Count(State.Shipped) / (Named - Count(State.Retired));
    }

    /// <summary>
    /// Computes the coverage from the catalogue's text and the shipped registry.
    /// </summary>
    /// <param name="catalogue">The full text of <c>docs/plan/08-rule-catalogue.md</c>.</param>
    /// <param name="shipped">Every id in <c>rules.json</c>.</param>
    /// <remarks>
    /// ⚠ A pure function of its two inputs, and it does no file IO. This assembly is loaded into
    /// the compiler and the IDE (docs/plan/01 § ADR-006); a metadata type that reads a path at
    /// runtime is a type that fails in the one host nobody tests.
    /// </remarks>
    public static Result Compute(string catalogue, IEnumerable<string> shipped) {
        if (catalogue is null) {
            throw new ArgumentNullException(nameof(catalogue));
        }

        var live = new HashSet<string>(shipped ?? [], StringComparer.Ordinal);
        var cut = CutWithAReason(catalogue);
        var retired = Retired(catalogue);

        var states = new SortedDictionary<string, State>(StringComparer.Ordinal);
        foreach (Match match in RuleId.Matches(catalogue)) {
            var id = match.Value;

            // ⚠ SK9xxx is the tool's own diagnostics — its own register, with its own guard in
            // ToolDiagnosticIdTests. Mixing them in would inflate the numerator with ids that were
            // never part of the rule plan.
            if (id.StartsWith("SK9", StringComparison.Ordinal)
                || IsBandEdge(id)
                || Array.IndexOf(RangeBoundaries, id) >= 0) {
                continue;
            }

            states[id] = live.Contains(id) ? State.Shipped
                : retired.Contains(id) ? State.Retired
                : cut.Contains(id) ? State.Cut
                : State.Outstanding;
        }

        return new Result { States = states };
    }

    /// <summary>
    /// The ids in § "Cut, with the reason" — the first cell of each row of that one table.
    /// </summary>
    /// <remarks>
    /// ⚠ Scoped to that section on purpose. § "Declared cut with no recorded reason" names another
    /// twelve, and the document's own position is that they are <em>outstanding</em>: a rule
    /// counted as cut when nobody recorded a reason is a decision nobody can review. Reading every
    /// id under a heading containing "cut" would quietly adopt the opposite position.
    /// </remarks>
    static HashSet<string> CutWithAReason(string catalogue) {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var section = Section(catalogue, "### Cut, with the reason");
        foreach (var line in section.Split('\n')) {
            if (!line.StartsWith("|", StringComparison.Ordinal)) {
                continue;
            }

            var cells = line.Split('|');
            if (cells.Length < 2) {
                continue;
            }

            var match = RuleId.Match(cells[1]);
            if (match.Success) {
                ids.Add(match.Value);
            }
        }

        return ids;
    }

    /// <summary>
    /// Ids the catalogue marks retired, spelled <c>`SKxxxx` is **retired</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ A retirement is a sentence in the document today because the registry has no row to hang
    /// it on: <c>RuleInfo.Retired</c> is a field on a rule, and an id retired before it was ever
    /// built has no rule. <c>allocated-ids.txt</c> records the allocation so the number cannot be
    /// handed out twice; this reads the disposal.
    /// </remarks>
    static HashSet<string> Retired(string catalogue) {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in new Regex(@"`(SK\d{4})` is \*\*retired").Matches(catalogue)) {
            ids.Add(match.Groups[1].Value);
        }

        return ids;
    }

    static string Section(string catalogue, string heading) {
        var start = catalogue.IndexOf(heading, StringComparison.Ordinal);
        if (start < 0) {
            return string.Empty;
        }

        var next = catalogue.IndexOf("\n### ", start + heading.Length, StringComparison.Ordinal);
        return next < 0 ? catalogue.Substring(start) : catalogue.Substring(start, next - start);
    }

    /// <summary>
    /// The Markdown that goes between the markers in doc 08.
    /// </summary>
    /// <remarks>
    /// ⚠ Deterministic, and no timestamp. A generated block that carries the moment it was
    /// generated produces a diff on every regeneration, and a diff that is always there is a diff
    /// nobody reads.
    /// </remarks>
    public static string Render(Result result) {
        if (result is null) {
            throw new ArgumentNullException(nameof(result));
        }

        var builder = new StringBuilder();
        builder.Append(BeginMarker).Append('\n');
        builder.Append("<!-- Regenerate with `skala rules docs`. Do not edit by hand: the numbers\n");
        builder.Append("     are computed from this file and rules.json, and a hand-kept count went\n");
        builder.Append("     stale inside one merge. -->\n\n");

        builder.Append("| | | |\n|---|---:|---|\n");
        Row(
            builder,
            "Rules this document names",
            result.Named,
            "excluding band edges (`SK1000`–`SK1999` and the like), `SK3499`/`SK3500`, and `SK9xxx`"
        );
        Row(
            builder,
            "**Shipped** — present in `rules.json`",
            result.Count(State.Shipped),
            "**" + result.Percentage.ToString("0.0", CultureInfo.InvariantCulture) + " %**"
        );

        Row(
            builder,
            "**Cut** — deliberately not built, reason recorded",
            result.Count(State.Cut),
            "§ \"Cut, with the reason\""
        );

        Row(
            builder,
            "**Retired** — allocated, superseded, never to be built",
            result.Count(State.Retired),
            "the id stays taken for ever (ADR-012)"
        );

        Row(
            builder,
            "**Outstanding** — planned, not built, not disposed of",
            result.Count(State.Outstanding),
            "includes the twelve declared cut with no reason recorded"
        );

        builder.Append('\n').Append(EndMarker);
        return builder.ToString();
    }

    static void Row(StringBuilder builder, string label, int count, string note) =>
        builder.Append("| ")
            .Append(label)
            .Append(" | **")
            .Append(count.ToString(CultureInfo.InvariantCulture))
            .Append("** | ")
            .Append(note)
            .Append(" |\n");

    /// <summary>
    /// Replaces the marked block in <paramref name="catalogue"/>, or returns null when the markers
    /// are absent.
    /// </summary>
    public static string? Replace(string catalogue, string block) {
        if (catalogue is null) {
            throw new ArgumentNullException(nameof(catalogue));
        }

        var start = catalogue.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = catalogue.IndexOf(EndMarker, StringComparison.Ordinal);
        return start < 0 || end < start
            ? null
            : catalogue.Substring(0, start) + block + catalogue.Substring(end + EndMarker.Length);
    }

    /// <summary>The block currently in the file, or null when the markers are absent.</summary>
    public static string? Current(string catalogue) {
        if (catalogue is null) {
            throw new ArgumentNullException(nameof(catalogue));
        }

        var start = catalogue.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = catalogue.IndexOf(EndMarker, StringComparison.Ordinal);
        return start < 0 || end < start
            ? null
            : catalogue.Substring(start, end + EndMarker.Length - start);
    }
}
