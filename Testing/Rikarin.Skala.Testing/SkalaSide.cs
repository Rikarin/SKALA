using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using System.Security.Cryptography;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
///     Skala's half of a key-flip measurement: one fixture, one option, one value.
/// </summary>
/// <remarks>
///     ⚠ <b>Why this is here and not in the sweep.</b> Two things now have to produce byte-identical
///     answers from the same three inputs: <c>KeyFlipSweep</c>, which measures against the oracle and
///     writes the committed table, and <c>ProvenanceTests.TheCommittedSweep_MeasuredTheFormatterInForce</c>,
///     which re-asks Skala alone and fails when the answer has moved. If those two ever drift apart the
///     drift test reports a formatter change on every run and means nothing.
///     <para>
///         ⚠ The repository has been bitten by exactly this: <c>OptionDomain</c>'s remarks record five
///         hand-kept copies of "the legal values of an option", four of which were invalidated at once by
///         giving int options a minimum. One implementation, two callers.
///     </para>
/// </remarks>
public static class SkalaSide {
    static readonly Lock Gate = new();
    static CSharpCompilation? arrangement;

    /// <summary>
    ///     Skala's answer for one option at one value, resolved from the repository's own chain.
    /// </summary>
    /// <remarks>
    ///     ⚠ Resolved from the fixture's real path and not from a copy in a scratch tree, which is both
    ///     cheaper and safer: <c>ConfigurationCache</c> memoises a parsed <c>.editorconfig</c> per path
    ///     with no eviction, and a fresh 294 KB copy per (option, value) would fill it with about a
    ///     thousand parses of the same document.
    ///     <para>
    ///         ⚠ Raw, and deliberately not normalised. Normalising here and not on the oracle side made
    ///         <c>skala_insert_final_newline</c> look <c>INERT</c> — the oracle moving and Skala
    ///         not — when <c>skala format --option</c> on the same fixture writes 12 bytes at <c>true</c>
    ///         and 11 at <c>false</c>. Both engines are asked the same question in the same units, and the
    ///         comparison normalises them together.
    ///     </para>
    /// </remarks>
    public static string Format(string fixturePath, string key, string value) =>
        Format(fixturePath, [new KeyValuePair<string, string>(key, value)]);

    /// <summary>
    ///     The same, with more than one key forced at once — the pairwise pass's grid.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>OptionResolver</c> applies the overrides last and in order, which is how both engines
    ///     reach the same configuration: the oracle is handed an appended <c>[*.cs]</c> section carrying
    ///     the same assignments in the same order, and an <c>.editorconfig</c>'s last assignment of a key
    ///     wins. A pair that assigned the same key twice would therefore be measuring its second value
    ///     only, which is why the pairwise plan refuses to pair a key with itself.
    /// </remarks>
    public static string Format(string fixturePath, IReadOnlyList<KeyValuePair<string, string>> overrides) {
        var resolved = OptionResolver.Resolve(fixturePath, overrides);

        // ⚠ A value error is an answer, not an exception. A probe set is built from the registry's
        // declared domain and the resolver may still refuse a value — an int outside its bounds, an
        // enum spelling the registry lists and the parser does not — and a run that threw on the
        // first one would measure nothing. Recording the refusal as the output keeps it comparable
        // across runs, and makes a newly-refused value show up as drift rather than as a crash.
        if (!resolved.ValueErrors.IsEmpty) {
            return "value-error: " + string.Join("; ", resolved.ValueErrors);
        }

        var text = CSharpFormatter.Read(fixturePath);
        if (!OracleProfile.For(fixturePath).IsSemantic) {
            return CSharpFormatter.Format(fixturePath, text, resolved.Options).Formatted;
        }

        // ⚠ The arrangement subtree is measured against the *cleanup* profile, so Skala's half of that
        // comparison is the arrange-and-format pipeline and not the formatter. Routed on the fixture's
        // path through the one authority both halves read, because the failure mode of two answers is
        // silent: `CSharpFormatter.Format` alone would return a merely-formatted file, the oracle would
        // return an arranged one, and every arrangement key would be reported DIVERGENT on a difference
        // no key caused.
        var result = ArrangementPipeline.Run(
            fixturePath,
            text,
            new PhaseOneOptions(resolved.Options),
            new ArrangementOptions(resolved.Options),
            ArrangementCompilation(),
            ArrangementDifferential.Removable(ArrangementCompilation(), fixturePath),
            filter: NeverPerformedUnlessAsked(overrides)
        );

        // ⚠ A file that did not reach a fixed point is not an answer, and scoring it as one is the
        // failure this whole comparison is exposed to: the oracle's side is a single `cleanupcode`
        // invocation, so comparing an unconverged Skala output against it would report the pipeline's
        // own incompleteness as a divergence of the key that was flipped. It is spelled into the
        // output rather than thrown, for the reason the value-error above is: a refusal that is
        // recorded stays comparable across runs.
        return result.Converged ? result.Text : "did-not-converge: " + result.Passes + " passes";
    }

    /// <summary>
    ///     The rewrites the oracle performs under no configuration at all (SK-DIV-0013, SK-DIV-0137), each
    ///     with the key that asks for it.
    /// </summary>
    static readonly (string Rule, string Key)[] NeverPerformed = [
        (ArrangeIds.NullCheckingPattern, "skala_null_checking_pattern_style"),
        (ArrangeIds.EmptyString, "skala_empty_string"),
        (ArrangeIds.RedundantBraces, "skala_braces_redundant")
    ];

    /// <summary>
    ///     Excludes each never-performed rewrite unless the configuration under measurement assigns
    ///     its own key.
    /// </summary>
    /// <remarks>
    ///     ⚠ #383. While <c>skala_empty_string = string_empty</c> did nothing, a <c>""</c> sitting in an
    ///     unrelated fixture cost nothing. Once it rewrote, <c>private string _text = "";</c> in
    ///     <c>accessor-owner.cs</c> turned five <c>skala_accessor_owner_body</c> and
    ///     <c>skala_local_function_body</c> rows from reproduces to wrong — a key that had not moved,
    ///     reported as broken because of a rewrite the oracle never performs under any key. A key-flip
    ///     row measures one key; a recorded divergence of another rule is not that key's answer.
    ///     <para>
    ///         Under its <em>own</em> key the rule still runs, so the row that measures it keeps
    ///         recording the divergence (<c>skala_null_checking_pattern_style = not_null_pattern</c>,
    ///         <c>skala_empty_string</c> at both values). This is the subset of
    ///         <see cref="ArrangementFilter.OracleComparable" /> that is about the oracle never moving —
    ///         not <c>Usings</c>, whose exclusion there is about the differential's references, and which
    ///         the sweep has always measured.
    ///     </para>
    /// </remarks>
    static ArrangementFilter NeverPerformedUnlessAsked(IReadOnlyList<KeyValuePair<string, string>> overrides) =>
        new(
            [],
            [
                .. NeverPerformed.Where(pair => !overrides.Any(o => string.Equals(
                            o.Key,
                            pair.Key,
                            StringComparison.Ordinal
                        )
                    )
                )
                    .Select(static pair => pair.Rule)
            ]
        );

    /// <summary>
    ///     The compilation the arrangement half is resolved against, built once.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>constructs/arrangement/</c> entire, and the same set the sweep hands its oracle — see
    ///     <see cref="Corpus.ArrangementConstructs" /> for why a subset answers differently. Built once
    ///     and shared: the input text of a fixture does not change across an option's values, only the
    ///     options do, so one compilation serves every configuration and re-parsing 27 files per
    ///     (option, value) would be the sweep's dominant Skala cost for no change in the answer.
    /// </remarks>
    static CSharpCompilation ArrangementCompilation() {
        lock (Gate) {
            return arrangement ??= ArrangementDifferential.Compile(Corpus.ArrangementConstructs());
        }
    }

    /// <summary>A short digest of one engine's output, so a table can be read as a diff.</summary>
    /// <remarks>
    ///     ⚠ Eight hex characters. Short enough to sit in a markdown column, and the population it has to
    ///     separate is the handful of distinct outputs one option produces across its own values — not a
    ///     corpus. It is a comparison aid and never a security claim.
    /// </remarks>
    public static string Digest(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8];
}
