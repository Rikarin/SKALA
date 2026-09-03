using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Tests;

public sealed class DistillTests {
    [Fact]
    public void Distilling_TheRealTemplate_RoundTrips() {
        // docs/plan/15 § M0, definition of done: distilling the export and re-resolving must
        // produce an identical effective option set. Over the real 4 238-line file, not a fixture.
        var probe = Path.Combine(RepositoryPaths.Root, "Probe.cs");
        var original = EditorConfigDocument.Load(RepositoryPaths.Template);
        var before = OptionResolver.Resolve(EditorConfigChain.Of(probe, original));

        var result = Distiller.Distill(original);
        var distilled = EditorConfigDocument.FromText(
            Path.Combine(RepositoryPaths.Root, "distilled.editorconfig"),
            result.Text
        );
        var after = OptionResolver.Resolve(EditorConfigChain.Of(probe, distilled));
        var dropped = result.DroppedKeys.ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < OptionRegistry.Count; i++) {
            var id = (OptionId)i;

            // ⚠ The invariant is the effective *value*, and only that. Milestone 0 also asserted
            // that `IsDefault` matched, which was true only while distill dropped nothing: a key it
            // drops is by construction one whose value the default supplies, so after distilling it
            // *is* a default and before it was not. Asserting otherwise asserts that distill does
            // nothing.
            Assert.Equal(before[id].Value, after[id].Value);

            var info = OptionRegistry.Get(id);
            var wasDropped = dropped.Contains(info.Key) || info.Aliases.Any(dropped.Contains);
            if (!wasDropped) {
                Assert.Equal(before[id].IsDefault, after[id].IsDefault);
            }
        }

        Assert.Equal(
            before.Options.GetText(OptionId.ResharperCsharpMaxLineLength),
            after.Options.GetText(OptionId.ResharperCsharpMaxLineLength)
        );
    }

    [Fact]
    public void Distilling_TheRealTemplate_DropsOnlyTheKeysWhoseDefaultWasChecked() {
        // ⚠ Until milestone 3 this asserted that distill dropped *nothing*, and that was the honest
        // state rather than a bug: JetBrains' EditorConfig property tables publish names, languages
        // and possible values and never a default, so no entry could be `resharper-docs`. M3 derived
        // the defaults from the oracle instead (docs/plan/03 § "Deriving ReSharper's defaults"), and
        // `oracle-probe` is evidence of the same kind — checked, rather than assumed.
        var result = Distiller.Distill(EditorConfigDocument.Load(RepositoryPaths.Template));

        Assert.True(result.Dropped > 0, "nothing was dropped; the derived default table is not reaching distill");
        // ⚠ Relative to what the template contains rather than an absolute floor: the count was
        // sized against a 4 238-line template and the author has since stripped it to 2 178.
        // What matters is that most keys are still retained for want of a verified default.
        Assert.True(
            result.RetainedUnverifiedDefault > result.Dropped,
            $"retained {result.RetainedUnverifiedDefault} against {result.Dropped} dropped"
        );

        // Still nothing claims the documentation as its source, because there is still no
        // documentation to claim.
        Assert.DoesNotContain(
            OptionRegistry.All,
            static info => info.DefaultSource == OptionDefaultSource.ReSharperDocs
        );

        // ⚠ Every key dropped is one whose default the oracle answered for. A key dropped on a
        // guessed default silently changes formatting in whoever's repository accepted the output.
        foreach (var key in result.DroppedKeys) {
            Assert.True(OptionRegistry.TryResolve(key, out var id), key);
            Assert.Equal(OptionDefaultSource.OracleProbe, OptionRegistry.Get(id).DefaultSource);
        }
    }

    [Theory]
    [InlineData(OptionDefaultSource.ReSharperDocs, "chop_if_long", true)]
    [InlineData(OptionDefaultSource.ReSharperDocs, "chop_always", false)]
    [InlineData(OptionDefaultSource.OracleProbe, "chop_if_long", true)]
    [InlineData(OptionDefaultSource.OracleProbe, "chop_always", false)]
    [InlineData(OptionDefaultSource.Template, "chop_if_long", false)]
    [InlineData(OptionDefaultSource.Unknown, "chop_if_long", false)]
    public void ADroppableKey_IsOnlyOneWhoseDefaultWasVerified(
        OptionDefaultSource source,
        string value,
        bool expected
    ) {
        // The rule that makes distill safe. A distill that drops a key on a guessed default
        // silently changes formatting, which is unacceptable.
        var info = OptionRegistry.Get(OptionId.ResharperCsharpWrapArgumentsStyle) with {
            Default = "chop_if_long", DefaultSource = source
        };

        Assert.Equal(expected, Distiller.ShouldDrop(info, value));
    }

    [Fact]
    public void ADroppableKey_IgnoresAMicrosoftSeveritySuffix() {
        var info = OptionRegistry.Get(OptionId.CsharpStyleNamespaceDeclarations) with {
            Default = "file_scoped:suggestion", DefaultSource = OptionDefaultSource.ReSharperDocs, SeveritySuffix = true
        };

        Assert.True(Distiller.ShouldDrop(info, "file_scoped:silent"));
        Assert.False(Distiller.ShouldDrop(info, "block_scoped:silent"));
    }

    [Fact]
    public void DistilledOutput_KeepsEveryKeyTheRegistryDoesNotOwn() {
        // The 3 021 inspection severities and the 215 naming keys are not Skala's to remove.
        var original = EditorConfigDocument.Load(RepositoryPaths.Template);
        var distilled = EditorConfigDocument.FromText("/repo/.editorconfig", Distiller.Distill(original).Text);

        var before = original.Assignments.Count(static a => a.Key.EndsWith("_highlighting", StringComparison.Ordinal));
        var after = distilled.Assignments.Count(static a => a.Key.EndsWith("_highlighting", StringComparison.Ordinal));

        Assert.Equal(before, after);
        // ⚠ Measured, not pinned — see EditorConfigIngestionTests. The severity keys the registry does
        // not own must survive distillation whatever the template happens to contain.
        var expected = File.ReadAllLines(RepositoryPaths.Template)
            .Count(static line => line.Contains("_highlighting", StringComparison.Ordinal));

        Assert.Equal(expected, after);
    }

    /// <summary>
    ///     ⚠ A comment stuck to a dropped key goes with it; every other comment stays.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>distill</c> dropped the assignment and left the comment above it, so the output described
    ///         a key that was no longer in the file. Invisible against the real export, where every comment
    ///         is a section banner and an orphan still reads as a heading — and actively misleading in a
    ///         configuration somebody annotated, which is the only kind of file the command exists to
    ///         produce.
    ///     </para>
    ///     <para>
    ///         The semantics are <c>skala_stick_comment</c>'s, already settled in this project for
    ///         code: a contiguous comment run belongs to the line directly beneath it, so a run followed by
    ///         a blank line, a section header or nothing is attached to no key and survives.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Distilling_TakesAStuckCommentWithTheKeyItDrops_AndKeepsEveryOther() {
        var original = EditorConfigDocument.Load(RepositoryPaths.AnnotatedEditorConfig);
        var result = Distiller.Distill(original);

        // The fixture only means anything if the key it is built around is actually dropped.
        Assert.Contains("skala_blank_lines_around_field", result.DroppedKeys);
        Assert.DoesNotContain(
            "skala_blank_lines_around_field",
            result.Text,
            StringComparison.Ordinal
        );

        Assert.DoesNotContain("STICKS TO A DROPPED KEY", result.Text, StringComparison.Ordinal);

        // ⚠ And nothing else went with it. Over-eager removal is the worse failure: an orphaned
        // comment is confusing, a deleted one is unrecoverable.
        Assert.Contains("STICKS TO A KEPT KEY", result.Text, StringComparison.Ordinal);
        Assert.Contains("DETACHED BY A BLANK LINE", result.Text, StringComparison.Ordinal);
        Assert.Contains("STICKS TO A SECTION HEADER", result.Text, StringComparison.Ordinal);
        Assert.Contains("TRAILING", result.Text, StringComparison.Ordinal);
        Assert.Contains("attached to nothing — a blank line follows it", result.Text, StringComparison.Ordinal);

        // The keys that were not at a verified default are all still there.
        Assert.Contains("skala_blank_lines_around_invocable = 3", result.Text, StringComparison.Ordinal);
        Assert.Contains("indent_size = 2", result.Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The comment rule must not change what the file means.
    /// </summary>
    /// <remarks>
    ///     Moving comment lines around a distiller that decides which assignments to keep is exactly
    ///     the sort of edit that quietly drops one, and a distilled file that resolves differently is
    ///     the failure the whole command is written to avoid.
    /// </remarks>
    [Fact]
    public void Distilling_TheAnnotatedFixture_ResolvesIdentically() {
        // ⚠ Both documents have to sit in the same directory as the probe, or the chain simply
        // does not reach one of them and the test compares a configuration against no
        // configuration — which passes or fails for a reason that has nothing to do with distilling.
        var directory = Path.GetDirectoryName(RepositoryPaths.AnnotatedEditorConfig)!;
        var probe = Path.Combine(directory, "Probe.cs");
        var original = EditorConfigDocument.Load(RepositoryPaths.AnnotatedEditorConfig);
        var before = OptionResolver.Resolve(EditorConfigChain.Of(probe, original));

        var distilled = EditorConfigDocument.FromText(
            Path.Combine(directory, "distilled.editorconfig"),
            Distiller.Distill(original).Text
        );

        var after = OptionResolver.Resolve(EditorConfigChain.Of(probe, distilled));

        foreach (var option in OptionRegistry.All) {
            Assert.Equal(before[option.Id].Value, after[option.Id].Value);
        }
    }
}
