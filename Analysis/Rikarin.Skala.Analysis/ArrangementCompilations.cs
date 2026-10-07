using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis;

/// <summary>
///     Which compilations semantic arrangement runs against, decided once for every verb that arranges.
/// </summary>
/// <remarks>
///     ⚠ #395. <c>verify</c> is <c>format --check</c> + <c>arrange --check</c> + <c>check --gate=local</c>,
///     and under <c>--load=loose</c> it was not: <c>verify</c>'s arrange stage handed
///     <see cref="ArrangeCommand" /> no compilation, <c>skala arrange</c> handed it the loose loader's, and
///     the same file was green under one verb and exit 2 under the other. Two call sites each deciding
///     the question is how they came apart, so <c>skala arrange</c>, <c>skala format --arrange=full</c> and
///     <c>verify</c> all ask this class and nothing else.
///     <para>
///         ⚠ <b>A loose load arranges the syntactic subset, and that was measured, not argued.</b> A loose
///         compilation is not any compilation the file belongs to: it has none of the project's
///         preprocessor symbols, binds against the running runtime's implementation assemblies rather
///         than the project's references, has no package references, no implicit or global usings, and —
///         for the single file an agent just wrote — none of the file's siblings. A semantic rule asks
///         "what does this bind to"; where the loose answer is an error type the rules decline, but where
///         it is a <em>different</em> symbol they rewrite confidently. Probes, each declined by the
///         workspace load of the same project and rewritten by the loose load of the same file:
///         <c>SK0210</c> deleted <c>using System.Text;</c> needed only under <c>#if NET8_0_OR_GREATER</c>
///         (a build break, <c>CS0246</c>); <c>SK0202</c> turned <c>long t = Now();</c> into
///         <c>var t</c> where the project's <c>Now()</c> returns <c>int</c> (a silent type change);
///         <c>SK0205</c> turned <c>h == null</c> into <c>h is null</c> past a user <c>operator ==</c>
///         declared under <c>#if</c>; <c>SK0211</c> turned <c>String</c> into <c>string</c> where a sibling
///         file declares <c>App.Text.String</c> (a public signature change). Over the vendored corpus
///         against a synthetic project per tree, the loose compilation also disagreed with the project
///         compilation on real code — see docs/plan/06 § "Usings" for the counts.
///     </para>
///     <para>
///         The reason is the loader, not the rules, so the decision is per load mode and not per rule: no
///         semantic rule can tell a loose binding that resolved to the wrong symbol from a right one.
///     </para>
/// </remarks>
public static class ArrangementCompilations {
    /// <summary>Whether a load in <paramref name="mode" /> runs the semantic half of arrangement.</summary>
    public static bool Semantic(LoadMode mode) => mode != LoadMode.Loose;

    /// <summary>
    ///     The compilations to hand <see cref="ArrangeRequest.Compilations" />: every loaded one, or none.
    /// </summary>
    /// <remarks>
    ///     ⚠ All of them, not the one that covers the first path — docs/plan/06 removes a using only when
    ///     it is unused in <em>every</em> compilation the file participates in. Empty is the documented
    ///     syntactic mode of <see cref="ArrangeCommand" />, not a failure.
    /// </remarks>
    public static IReadOnlyList<CSharpCompilation> For(LoadedProject loaded) =>
        Semantic(loaded.Mode) ? [.. loaded.Units.Select(static unit => unit.Compilation)] : [];

    /// <summary>The arrangement rules a load in <paramref name="mode" /> does not run, with the reason.</summary>
    /// <remarks>
    ///     ⚠ <c>SK0210</c> is listed although its sorting half still runs: removing an unused using is the
    ///     half that needs the compilation, and a SKIPPED list that left it out was the silent half of
    ///     #395 — <c>verify</c> did not remove under a loose load and did not say so either.
    /// </remarks>
    public static ImmutableArray<SkippedRule> SkippedFor(LoadMode mode) {
        if (Semantic(mode)) {
            return [];
        }

        return [
            .. Arranger.Rules()
                .Where(static rule => rule.NeedsSemantics)
                .Select(static rule => rule.Id)
                .Distinct(StringComparer.Ordinal)
                .Select(static id => new SkippedRule(
                        id,
                        "arrangement requires the project's semantic model; --load=loose binds the file against "
                        + "something else"
                    )
                ),
            new SkippedRule(
                ArrangeIds.Usings,
                "unused-using removal requires the project's semantic model; --load=loose sorts usings only"
            )
        ];
    }
}
