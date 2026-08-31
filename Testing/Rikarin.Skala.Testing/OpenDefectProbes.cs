using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace Rikarin.Skala.Testing;

/// <summary>
///     One register entry's trigger, as an edit that removes it and nothing else.
/// </summary>
/// <remarks>
///     ⚠ <b>A probe is not a description of a defect; it is the removal of its cause.</b> That is the
///     whole reason this type exists rather than a string field. Matching a fuzz finding against an open
///     defect by rule id, property name or minimised bytes is a claim nobody checks — the property name
///     is shared by every idempotence bug there will ever be, and two seeds minimise the same defect to
///     two different files — measured on the now-retired SK-FUZZ-0016, whose fixture was 46 bytes and
///     whose rediscovery by nightly 33207471534 minimised to 81. A probe makes the claim checkable:
///     delete the trigger this entry names, ask the oracle again, and believe the entry only if the
///     property now <i>holds</i>.
///     <para>
///         ⚠ <see cref="Neutralise" /> returns <c>null</c> when the trigger is not in the input at all,
///         and otherwise the input with it removed. It must remove the trigger and as little else as it
///         can: an over-broad probe is the one way this mechanism can go wrong, because an edit that
///         deletes half the file makes every property hold and would account for anything.
///         <see cref="OpenDefects.Explain" /> refuses a neutralisation that changes nothing and one that
///         makes the input parse worse, and <c>OpenDefectTests</c> requires every probe to fire on its
///         own entry's fixture — but neither of those can catch a probe that is merely too greedy, so
///         the vocabulary below is closed, small, and each entry argues for itself.
///     </para>
/// </remarks>
public sealed record OpenDefectProbe(string Name, string What, Func<string, string?> Neutralise);

/// <summary>
///     The closed vocabulary of triggers a register entry may name in its <c>probe:</c> field.
/// </summary>
/// <remarks>
///     ⚠ Closed on purpose. An entry may only name a probe that exists here, and adding one is a code
///     change that has to be argued in review beside the register entry it serves — which is the
///     difference between this and a suppression list, where a new line silences a new failure and
///     nobody has to say why. The register makes the argument against the suppression-list shape
///     itself, in the entry that prompted this mechanism: "a suppression list keyed on the defect the
///     fuzzer is <i>for</i> would hide the next variant too." It would. This does not, because the next
///     variant still fails the property after the neutralisation and is reported as new.
/// </remarks>
public static class OpenDefectProbes {
    /// <summary>
    ///     ⚠ A <c>///</c> run, which routes the gap between its lines through the doc-comment
    ///     sub-formatter. Demoting the run to <c>//</c> takes that sub-formatter out of the case while
    ///     leaving the comment, its text and every line ending exactly where they were — which is the
    ///     third of the three probes SK-FUZZ-0015 records having already run by hand.
    /// </summary>
    public const string DocCommentRun = "doc-comment-run";

    public static ImmutableArray<OpenDefectProbe> All { get; } = [
        new OpenDefectProbe(
            DocCommentRun,
            "every line-leading `///` demoted to `//`",
            DemoteDocCommentRuns
        )
    ];

    public static OpenDefectProbe? Find(string name) =>
        All.FirstOrDefault(probe => string.Equals(probe.Name, name, StringComparison.Ordinal));

    // ⚠ Line-leading only. A blanket `source.Replace("///", "//")` also rewrites the inside of string
    // literals, and a probe that corrupts a literal can make a property hold for a reason that has
    // nothing to do with the defect it claims to characterise.
    static string? DemoteDocCommentRuns(string source) {
        var lines = source.Split('\n');
        var found = false;
        for (var index = 0; index < lines.Length; index++) {
            var line = lines[index];
            var indent = line.Length - line.AsSpan().TrimStart(" \t").Length;
            if (!line.AsSpan(indent).StartsWith("///", StringComparison.Ordinal)) {
                continue;
            }

            found = true;
            lines[index] = line[..indent] + "//" + line[(indent + 3)..];
        }

        return found ? string.Join('\n', lines) : null;
    }

    /// <summary>
    ///     Whether <paramref name="after" /> parses no worse than <paramref name="before" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Relative and not absolute, because a fuzz finding is often already unparseable in places
    ///     and demanding a clean parse would refuse every probe on the inputs that need one. What must
    ///     not happen is a probe that <i>breaks</i> the file: ADR-003 leaves an input that lost its
    ///     parse byte-identical, so every property holds over it for free — which would make a broken
    ///     neutralisation look exactly like a successfully characterised defect.
    /// </remarks>
    public static bool ParsesNoWorse(string before, string after) => Errors(after) <= Errors(before);

    static int Errors(string source) =>
        CSharpSyntaxTree.ParseText(source)
            .GetDiagnostics()
            .Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
