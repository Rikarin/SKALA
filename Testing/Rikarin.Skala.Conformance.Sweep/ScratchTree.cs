using System.Globalization;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>
///     One <c>cleanupcode</c> invocation over a directory per fixture, each with its own configuration.
/// </summary>
/// <remarks>
///     ⚠ This shape, and not a shared <c>.editorconfig</c>, is what makes a batched run answer a
///     question about one option rather than about a configuration. Batching by value index is the only
///     affordable arrangement — <c>cleanupcode</c>'s startup is tens of seconds and ~950 configurations
///     one at a time is not viable — but with one config for the whole batch every fixture is moved by
///     every other option in it. M3's first attempt at exactly this came back "197 options set, 0
///     fixtures unchanged". A directory per fixture, each carrying its own <c>root = true</c> and its
///     own single override, gives the batching for free and the isolation with it.
///     <para>
///         ⚠ The result is index-aligned with the batch, and a slot is <see langword="null" /> when
///         <c>cleanupcode</c> produced nothing for it. A missing output is a hole in the measurement;
///         callers must not score it as agreement. Bodies come back exactly as the tool wrote them —
///         normalising here would erase the whole effect of the line-ending and final-newline options.
///     </para>
/// </remarks>
public static class ScratchTree {
    /// <summary>
    ///     Which <c>cleanupcode</c> profile can move this fixture at all.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Measured, on master, the day the doc-comment profile landed.</b> The sweep ran one profile
    ///     — <c>CSReformatCode</c> — and that profile switches <c>CSharpFormatDocComments</c> off, so on a
    ///     doc-comment fixture the oracle <em>cannot</em> move whatever key is flipped. Skala formats doc
    ///     comments by default, so it does move, and the verdict for every such option is
    ///     <c>SPURIOUS</c>: "Skala reacts where ReSharper does not". All 13 doc-comment keys promoted to
    ///     Tier A came back <c>SPURIOUS</c> on the first sweep after the merge, and
    ///     <c>OptionCoverageTests</c> then demanded they be demoted.
    ///     <para>
    ///         ⚠ That demotion would have been the wrong fix, and the failure message asked for it. The
    ///         options were measured correctly under the profile that can see them; the sweep was asking
    ///         the wrong question. A verdict is only about an option when the oracle was given a profile
    ///         that lets it answer.
    ///     </para>
    /// </remarks>
    public static OracleProfile ProfileFor(CorpusFile fixture) =>
        fixture.RelativePath.StartsWith(Corpus.XmlDocPrefix, StringComparison.Ordinal)
            ? OracleProfile.DocComments
            : OracleProfile.FormatOnly;

    /// <summary>
    ///     ⚠ Every fixture in one batch must want the same profile: a batch is one <c>cleanupcode</c>
    ///     invocation and an invocation carries one profile. Callers partition by this before batching.
    /// </summary>
    public static IEnumerable<IGrouping<OracleProfile, T>> ByProfile<T>(
        IEnumerable<T> items,
        Func<T, CorpusFile> fixture
    ) =>
        items.GroupBy(item => ProfileFor(fixture(item)));

    public static string?[] Format(
        OracleRunner runner,
        IReadOnlyList<SweepCandidate> batch,
        Func<SweepCandidate, string> config
    ) =>
        Format(runner, [.. batch.Select(static candidate => candidate.Fixture)], i => config(batch[i]));

    /// <summary>
    ///     The same, addressed by fixture and index rather than by <see cref="SweepCandidate" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ The pairwise pass writes <em>two</em> overrides into a directory's <c>.editorconfig</c> and
    ///     has no single option to name it by, so the batch it hands over is a list of fixtures and the
    ///     configuration is a function of the slot. Both overloads share this body deliberately: the
    ///     directory-per-fixture isolation is the property that makes any batched run answer a question
    ///     about its own configuration, and a second copy of it is a second chance to lose it.
    /// </remarks>
    public static string?[] Format(
        OracleRunner runner,
        IReadOnlyList<CorpusFile> batch,
        Func<int, string> config
    ) {
        var scratch = Directory.CreateTempSubdirectory("skala-sweep-");
        try {
            File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.csproj"), OracleRunner.ProjectFile);
            File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.sln"), OracleRunner.SolutionFile);

            var produced = new string[batch.Count];
            for (var i = 0; i < batch.Count; i++) {
                var directory = Path.Combine(scratch.FullName, "d" + i.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, ".editorconfig"), config(i));
                produced[i] = Path.Combine(directory, "F.cs");
                File.Copy(batch[i].Path, produced[i]);
            }

            // ⚠ Raw, not normalised. The caller decides — and it must, because
            // `resharper_enforce_line_ending_style` and `resharper_csharp_insert_final_newline`
            // change nothing that survives normalisation.
            // ⚠ One profile per invocation, chosen from the batch rather than hard-coded. Every
            // member of a batch is asserted to want the same one, because a batch mixing profiles
            // would silently measure half of it under the wrong question — see ProfileFor.
            var profile = ProfileFor(batch[0]);
            for (var i = 1; i < batch.Count; i++) {
                if (ProfileFor(batch[i]) != profile) {
                    throw new InvalidOperationException(
                        "a batch mixes oracle profiles: "
                        + batch[0]
                        + " wants "
                        + profile.Name
                        + " and "
                        + batch[i]
                        + " wants "
                        + ProfileFor(batch[i]).Name
                        + ". Partition with ScratchTree.ByProfile before batching."
                    );
                }
            }

            var bodies = runner.FormatInPlace(scratch.FullName, produced, profile);
            var results = new string?[batch.Count];
            for (var i = 0; i < batch.Count; i++) {
                results[i] = bodies.GetValueOrDefault(produced[i]);
            }

            return results;
        } finally {
            try {
                scratch.Delete(recursive: true);
            } catch (IOException) {
                // A scratch directory the tool still holds open is not worth failing a sweep over.
            }
        }
    }
}
