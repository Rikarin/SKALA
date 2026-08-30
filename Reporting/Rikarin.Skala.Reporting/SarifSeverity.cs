using Microsoft.CodeAnalysis.Sarif;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Reporting;

/// <summary>
///     The one place Skala's severity vocabulary turns into SARIF's, and back.
/// </summary>
/// <remarks>
///     <para>
///         Skala has four severities and SARIF has three failure levels, so the mapping is lossy in one
///         direction and has to be written down rather than inferred at each call site. It was inferred
///         at three of them, and they disagreed: the writer sent <c>hint</c> to
///         <see cref="FailureLevel.None" />, the reader read <see cref="FailureLevel.Note" /> back as
///         <see cref="SkalaSeverity.Info" />, and a run round-tripped through <c>skala report</c> came
///         back with its hints and its suggestions merged.
///     </para>
///     <list type="table">
///         <listheader>
///             <term>Skala</term>
///             <description>SARIF <c>level</c> / <c>kind</c>, and why</description>
///         </listheader>
///         <item>
///             <term><see cref="SkalaSeverity.Error" /> (<c>error</c>)</term>
///             <description><c>error</c>, <c>fail</c>.</description>
///         </item>
///         <item>
///             <term><see cref="SkalaSeverity.Warning" /> (<c>warning</c>)</term>
///             <description><c>warning</c>, <c>fail</c>.</description>
///         </item>
///         <item>
///             <term><see cref="SkalaSeverity.Info" /> (<c>suggestion</c>)</term>
///             <description><c>note</c>, <c>fail</c>.</description>
///         </item>
///         <item>
///             <term><see cref="SkalaSeverity.Hidden" /> (<c>hint</c>)</term>
///             <description>
///                 <c>note</c>, <c>fail</c> — <b>not <c>none</c></b>. SARIF 2.1.0 § 3.27.10 permits
///                 <c>level: none</c> only on a result whose <c>kind</c> is something other than
///                 <c>fail</c>; a hint is a rule violation, so its <c>kind</c> is <c>fail</c> and
///                 <c>none</c> is not available to it. Skala emitted <c>none</c> on 249 of the 446
///                 results in its own report, which is SARIF the spec does not allow and which GitHub's
///                 documented vocabulary (<c>error</c>, <c>warning</c>, <c>note</c>) has no rendering
///                 for. ⚠ <c>note</c> is the floor of SARIF's failure scale, so <c>hint</c> and
///                 <c>suggestion</c> land on the same level; <see cref="Property" /> is what keeps the
///                 distinction, and what <see cref="SarifReader" /> reads to get the exact severity back.
///             </description>
///         </item>
///     </list>
///     <para>
///         ⚠ <see cref="RuleSeverity.None" /> — "suppressed, never runs, never reported" — is not a
///         level at all and never reaches a result. On a rule descriptor it is
///         <c>defaultConfiguration.enabled: false</c>, which is SARIF's own way of saying it; see
///         <see cref="Configuration" />.
///     </para>
///     <para>
///         ⚠ <c>result.baselineState</c> is deliberately <em>not</em> used to carry
///         <see cref="BaselineBucket" />. SARIF § 3.27.24 makes it conditional on the run carrying a
///         <c>baselineGuid</c>, and inventing a stable GUID for <c>.skala/baseline.sarif</c> is a
///         separate decision. The bucket travels in <c>properties.baseline</c>, as it always has, and
///         what a consumer acts on is the <c>suppressions</c> entry — see
///         <see cref="SarifWriter.BaselineJustification" />.
///     </para>
/// </remarks>
public static class SarifSeverity {
    /// <summary>
    ///     The result property carrying the exact Skala severity word.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is what makes the mapping lossless in the direction that matters. Without it
    ///     <c>skala report</c> over a stored SARIF cannot tell a hint from a suggestion, and
    ///     <see cref="History" />'s hint count — which is a recorded, compared number — silently folds
    ///     into the suggestion count.
    /// </remarks>
    public const string Property = "skalaSeverity";

    /// <summary>Skala's word for a severity: <c>error</c>, <c>warning</c>, <c>suggestion</c>, <c>hint</c>.</summary>
    public static string Word(SkalaSeverity severity) => Renderer.Word(severity);

    /// <summary>The SARIF level a Skala severity is reported at. See the table on this type.</summary>
    public static FailureLevel Level(SkalaSeverity severity) =>
        severity switch {
            SkalaSeverity.Error => FailureLevel.Error,
            SkalaSeverity.Warning => FailureLevel.Warning,

            // ⚠ Both `Info` and `Hidden`. SARIF's failure scale bottoms out at `note`, and `none` is
            // reserved for results that are not failures at all.
            _ => FailureLevel.Note
        };

    /// <summary>The SARIF level a rule's configured default severity is reported at.</summary>
    /// <remarks>
    ///     ⚠ Must agree with <see cref="Level(SkalaSeverity)" /> term for term: a rule whose
    ///     <c>defaultConfiguration.level</c> disagrees with the level its own results carry is a rules
    ///     table that cannot be used to explain the results.
    /// </remarks>
    public static FailureLevel Level(RuleSeverity severity) =>
        severity switch {
            RuleSeverity.Error => FailureLevel.Error,
            RuleSeverity.Warning => FailureLevel.Warning,
            RuleSeverity.Hint or RuleSeverity.Suggestion => FailureLevel.Note,

            // `None` is not a level; the descriptor says so with `enabled: false` instead.
            _ => FailureLevel.None
        };

    /// <summary>A rule's default configuration, level and enablement together.</summary>
    public static ReportingConfiguration Configuration(RuleSeverity severity) =>
        new() { Enabled = severity != RuleSeverity.None, Level = Level(severity) };

    /// <summary>
    ///     The severity a result was written at, read back.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="Property" /> wins where it is present, because it is exact. The level is the
    ///     fallback, and it is the only thing a foreign SARIF offers — there <c>none</c> genuinely does
    ///     mean "below note", so it reads back as <see cref="SkalaSeverity.Hidden" />.
    /// </remarks>
    public static SkalaSeverity Read(string? word, FailureLevel level) =>
        word switch {
            "error" => SkalaSeverity.Error,
            "warning" => SkalaSeverity.Warning,
            "suggestion" => SkalaSeverity.Info,
            "hint" => SkalaSeverity.Hidden,
            _ => level switch {
                FailureLevel.Error => SkalaSeverity.Error,
                FailureLevel.Warning => SkalaSeverity.Warning,
                FailureLevel.Note => SkalaSeverity.Info,
                _ => SkalaSeverity.Hidden
            }
        };
}
