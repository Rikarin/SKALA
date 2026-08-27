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
        Assert.True(result.RetainedUnverifiedDefault > 300);

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
        Assert.Equal(3021, after);
    }
}
