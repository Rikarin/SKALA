using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     ⚠ #395: the one decision every arranging verb takes about which compilations semantic
///     arrangement may bind against.
/// </summary>
public sealed class ArrangementCompilationsTests {
    static readonly CSharpCompilation Empty = CSharpCompilation.Create("probe");

    [Theory]
    [InlineData(LoadMode.Loose, false)]
    [InlineData(LoadMode.Workspace, true)]
    [InlineData(LoadMode.Binlog, true)]
    public void For_HandsOverEveryUnit_ExceptUnderALooseLoad(LoadMode mode, bool semantic) {
        var loaded = new LoadedProject {
            Mode = mode,
            Units = [
                new CompilationUnit { Name = "a", Compilation = Empty },
                new CompilationUnit { Name = "b", Compilation = Empty.WithAssemblyName("other") }
            ]
        };

        Assert.Equal(semantic ? 2 : 0, ArrangementCompilations.For(loaded).Count);
        Assert.Equal(semantic, ArrangementCompilations.Semantic(mode));
    }

    /// <summary>
    ///     What a loose load does not run is what it says it does not run: every semantic rule, and the
    ///     removal half of <c>SK0210</c>, which declares no semantics because its sorting half needs none.
    /// </summary>
    [Fact]
    public void SkippedFor_ALooseLoad_NamesEverySemanticRuleAndUsingRemoval() {
        var expected = Arranger.Rules()
            .Where(static rule => rule.NeedsSemantics)
            .Select(static rule => rule.Id)
            .Append(ArrangeIds.Usings)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        var skipped = ArrangementCompilations.SkippedFor(LoadMode.Loose)
            .Select(static rule => rule.RuleId)
            .Order(StringComparer.Ordinal);

        Assert.Equal(expected, skipped);
        Assert.Empty(ArrangementCompilations.SkippedFor(LoadMode.Workspace));
        Assert.Empty(ArrangementCompilations.SkippedFor(LoadMode.Binlog));
    }

    /// <summary>
    ///     ⚠ A loose <c>verify</c> listed all ten semantic arrangement ids twice — once as a
    ///     <c>requiresSemantics</c> analyzer, once as an arrangement rule — and counted them twice.
    /// </summary>
    [Fact]
    public void LooseVerify_ListsEachSkippedRuleOnce_IncludingUsingRemoval() {
        using var scratch = new Scratch();
        scratch.Write("F.cs", "namespace P;\n\npublic sealed class F;\n");

        var (_, report) = CheckCommand.Run(
            new() {
                RepositoryRoot = scratch.Root,
                Paths = [scratch.Root],
                Mode = LoadMode.Loose,
                Output = string.Empty,
                NoCache = true,
                IncludeArrangement = true
            },
            TestContext.Current.CancellationToken
        );

        var ids = report.SkippedRules.Select(static rule => rule.RuleId).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(ArrangeIds.Usings, ids);
        Assert.Contains(ArrangeIds.Var, ids);
    }
}
