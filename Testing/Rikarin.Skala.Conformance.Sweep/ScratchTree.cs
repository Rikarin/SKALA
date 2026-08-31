using Rikarin.Skala.Testing;
using System.Globalization;

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
    ///     How many <c>cleanupcode</c> invocations a semantic batch may take to stop moving.
    /// </summary>
    /// <remarks>
    ///     ⚠ Four, the same bound <c>ArrangementPipeline.MaxPasses</c> gives Skala's side, so that
    ///     neither engine is allowed more passes than the other before the comparison is declared
    ///     unreadable. The observed cost is two.
    /// </remarks>
    public const int MaxOraclePasses = 4;

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
    /// <remarks>
    ///     ⚠ The decision itself now lives in <see cref="OracleProfile.For(CorpusFile)" />, because
    ///     Skala's half of the same measurement has to reach the same answer — see that method. This
    ///     stays as the name the sweep's call sites and its tests already use.
    /// </remarks>
    public static OracleProfile ProfileFor(CorpusFile fixture) => OracleProfile.For(fixture);

    /// <summary>
    ///     ⚠ Every fixture in one batch must want the same profile: a batch is one <c>cleanupcode</c>
    ///     invocation and an invocation carries one profile. Callers partition by this before batching.
    /// </summary>
    public static IEnumerable<IGrouping<OracleProfile, T>> ByProfile<T>(
        IEnumerable<T> items,
        Func<T, CorpusFile> fixture
    ) =>
        items.GroupBy(item => ProfileFor(fixture(item)));

    /// <summary>
    ///     One profile's members, cut into batches an invocation can answer honestly.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         A size limit is not the only constraint, and under the cleanup profile it is not the
    ///         binding one.
    ///     </b> Members are batched by count for a whitespace profile, because a
    ///     <c>cleanupcode</c> run that only moves whitespace cannot be affected by what else is in the
    ///     project. A <em>semantic</em> profile resolves symbols, and the sweep's shape guarantees the
    ///     collision: 44 arrangement keys point at 22 fixtures, so four keys name
    ///     <c>redundancy/qualifiers-and-parentheses.cs</c> and a count-batched round would copy that one
    ///     file into four directories of one project — four declarations of
    ///     <c>
    /// class
    ///     QualifiersAndParentheses
    ///     </c> in one namespace. Every semantic rewrite in the profile
    ///     (<c>var</c>, qualifiers, predefined types) then reads a compilation full of CS0101, and the
    ///     verdicts would be a measurement of the scratch project.
    ///     <para>
    ///         ⚠ So a semantic batch holds each fixture at most once, and the four keys that share a
    ///         fixture are answered in four invocations rather than one. That is the cost —
    ///         <c>cleanupcode</c> startup, several times per round — and it is the price of the answer
    ///         being about the option.
    ///     </para>
    /// </remarks>
    public static IEnumerable<IReadOnlyList<T>> Batches<T>(
        IReadOnlyList<T> members,
        Func<T, CorpusFile> fixture,
        OracleProfile profile,
        int size
    ) {
        if (!profile.IsSemantic) {
            for (var start = 0; start < members.Count; start += size) {
                yield return [.. members.Skip(start).Take(size)];
            }

            yield break;
        }

        var pending = new List<T>(members);
        while (pending.Count > 0) {
            var batch = new List<T>();
            var taken = new HashSet<string>(StringComparer.Ordinal);
            for (var i = pending.Count - 1; i >= 0; i--) {
                if (batch.Count >= size || !taken.Add(fixture(pending[i]).Path)) {
                    continue;
                }

                batch.Add(pending[i]);
                pending.RemoveAt(i);
            }

            batch.Reverse();
            yield return batch;
        }
    }

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

            Context(scratch.FullName, batch, profile);

            var bodies = Converge(runner, scratch.FullName, produced, profile);
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

    /// <summary>
    ///     The oracle's output, run until it stops moving — as many invocations as that takes.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Because the two sides of the comparison have to be stopped at the same place.</b> Skala's
    ///     half of a cleanup-profile verdict is <c>ArrangementPipeline</c>, which loops: a rewrite can
    ///     expose a rewrite that was not available before it. If the oracle's half were one invocation,
    ///     any fixture needing two would show Skala having gone further, and the sweep would report the
    ///     difference as the flipped key diverging.
    ///     <para>
    ///         ⚠ <b>Measured, not assumed, and it is not hypothetical.</b> <c>sweep fixed-point</c> runs
    ///         <c>cleanupcode</c> over <c>constructs/arrangement/</c> and then again over its own output:
    ///         27 of 27 files move on the first pass and <em>one</em> — <c>namespaces/file-scoped.cs</c> —
    ///         moves again on the second, where converting a block-scoped namespace to a file-scoped one
    ///         leaves a blank first line that only a further invocation removes. That fixture is the one
    ///         <c>csharp_style_namespace_declarations</c> is pinned by, so a single-invocation oracle would
    ///         have manufactured a divergence for exactly the key whose fixture exposes the defect.
    ///     </para>
    ///     <para>
    ///         ⚠ The confirming invocation is not free and cannot be avoided: "it did not move" is only
    ///         knowable by running it again. That is the same bargain Skala's pipeline strikes — it reports
    ///         two passes on all 27, one working and one proving.
    ///     </para>
    ///     <para>
    ///         ⚠ Whitespace profiles are left at one invocation deliberately. <c>CSReformatCode</c> is a
    ///         single document build and emit, every measurement in milestones 1–3 rests on its being
    ///         idempotent, and looping it would double the cost of 855 configurations to re-answer a
    ///         settled question.
    ///     </para>
    ///     <para>
    ///         ⚠ A batch still moving at the bound is recorded as such in the output rather than returned
    ///         as though it had settled — the same sentinel <c>SkalaSide</c> writes when the pipeline does
    ///         not converge, and for the same reason. Silently handing back the last of four unconverged
    ///         passes is how a measurement bug becomes a table.
    ///     </para>
    /// </remarks>
    static IReadOnlyDictionary<string, string> Converge(
        OracleRunner runner,
        string root,
        IReadOnlyList<string> produced,
        OracleProfile profile
    ) {
        var bodies = runner.FormatInPlace(root, produced, profile);
        if (!profile.IsSemantic) {
            return bodies;
        }

        for (var pass = 1; pass < MaxOraclePasses; pass++) {
            var again = runner.FormatInPlace(root, produced, profile);
            if (Settled(bodies, again)) {
                return again;
            }

            bodies = again;
        }

        var final = runner.FormatInPlace(root, produced, profile);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in produced) {
            if (!final.TryGetValue(path, out var body)) {
                continue;
            }

            result[path] = Settled(bodies, final)
                || !bodies.TryGetValue(path, out var previous)
                || string.Equals(previous, body, StringComparison.Ordinal)
                    ? body
                    : "did-not-converge: " + MaxOraclePasses.ToString(CultureInfo.InvariantCulture) + " oracle passes";
        }

        return result;
    }

    static bool Settled(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after) {
        if (before.Count != after.Count) {
            return false;
        }

        foreach (var (path, body) in after) {
            if (!before.TryGetValue(path, out var previous)
                || !string.Equals(previous, body, StringComparison.Ordinal)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     The rest of the subtree a semantic profile needs in the project in order to resolve.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Without this the sweep measures the scratch project.</b>
    ///     <c>constructs/arrangement/usings/sort-and-remove.cs</c> opens with <c>using Alpha.Things;</c>,
    ///     and <c>Alpha.Things</c> exists because <c>constructs/arrangement/usings/namespaces.cs</c>
    ///     declares it. Copy the first file into a project without the second and the import does not
    ///     resolve, so <c>CSOptimizeUsings</c> deletes it whatever <c>resharper_sort_usings</c> says —
    ///     an oracle that never varies, which
    ///     <see cref="KeyFlipSweep.IsUnvaryingRound" /> would report and
    ///     <see cref="OptionSweep.Classify" /> would call <c>SPURIOUS</c>. Both readings would be about
    ///     this directory and not about the key.
    ///     <para>
    ///         ⚠ The whole subtree, every round, so that the compilation the oracle answers in is the same
    ///         one in every round and the same one <c>SkalaSide</c> compiles — see
    ///         <see cref="Corpus.ArrangementConstructs" />. A member of the batch is skipped here, because
    ///         it is already in the project under its own <c>.editorconfig</c>; a context file gets a bare
    ///         <c>root = true</c>, because its output is never read and only its declarations matter.
    ///     </para>
    ///     <para>
    ///         ⚠ A directory each rather than one shared one: two corpus files can have the same base name
    ///         under different construct folders, and a name collision here would silently drop a
    ///         declaration.
    ///     </para>
    /// </remarks>
    static void Context(string root, IReadOnlyList<CorpusFile> batch, OracleProfile profile) {
        if (!profile.IsSemantic) {
            return;
        }

        var already = batch.Select(static file => file.Path).ToHashSet(StringComparer.Ordinal);
        var index = 0;
        foreach (var file in Corpus.ArrangementConstructs()) {
            if (!already.Add(file.Path)) {
                continue;
            }

            var directory = Path.Combine(root, "c" + index.ToString(CultureInfo.InvariantCulture));
            index++;
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, ".editorconfig"), "root = true\n");
            File.Copy(file.Path, Path.Combine(directory, "F.cs"));
        }
    }
}
