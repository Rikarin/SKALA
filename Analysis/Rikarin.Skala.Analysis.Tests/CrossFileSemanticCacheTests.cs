using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #516: a <c>Semantic</c> rule's answer for a file that did not change is recomputed when another
///     file in the compilation changes what it binds to.
/// </summary>
/// <remarks>
///     ⚠ The issue's reproduction, in-process. <c>A.cs</c> never changes; <c>B.cs</c> turns
///     <c>Holder.L</c> from a <c>List&lt;int&gt;</c> into a user type with its own <c>ToArray()</c>, where
///     <c>SK1133</c>'s spread is not the same program. Before the fix the warm run served <c>A.cs</c>'s
///     cached <c>SK1133</c> — the per-file key hashed <c>A.cs</c>'s text and the compilation's options and
///     references, never <c>B.cs</c> — and <c>skala fix</c> would have rewritten the call into a spread
///     over a type that is not that collection.
///     <para>
///         ⚠ Every warm assertion is paired with a hit count, because a run that went cold would pass the
///         finding assertions for the wrong reason (#363).
///     </para>
/// </remarks>
[Collection(SerialWorkspace.Name)]
public sealed class CrossFileSemanticCacheTests {
    const string CollectionExpressionSpread = "SK1133";

    const string EmptyCatch = "SK2014";

    const string Project = """
                           <Project Sdk="Microsoft.NET.Sdk">
                             <PropertyGroup>
                               <TargetFramework>net10.0</TargetFramework>
                               <LangVersion>14.0</LangVersion>
                               <Nullable>enable</Nullable>
                             </PropertyGroup>
                           </Project>
                           """;

    /// <summary>Never changes. Its <c>SK2014</c> is a Syntax-scoped finding the warm run may serve.</summary>
    const string A = """
                     using System.Linq;

                     public static class A {
                         public static int[] M() {
                             int[] a = Holder.L.ToArray();
                             try {
                                 System.Console.WriteLine();
                             } catch {
                             }

                             return a;
                         }
                     }
                     """;

    const string BList = """
                         using System.Collections.Generic;

                         public static class Holder {
                             public static List<int> L = [];
                         }
                         """;

    const string BBox = """
                        public sealed class Box {
                            public int[] ToArray() => [];
                        }

                        public static class Holder {
                            public static Box L = new();
                        }
                        """;

    [Fact]
    public void ADeclarationChangeInAnotherFile_RecomputesTheUnchangedFilesSemanticFinding() {
        using var scratch = new Scratch();
        var project = scratch.Write("Probe.csproj", Project);
        scratch.Write("Directory.Build.props", "<Project />");
        scratch.Write("Directory.Build.targets", "<Project />");
        scratch.Write("A.cs", A);
        scratch.Write("B.cs", BList);

        var cold = Run(scratch, project, true);
        Assert.Equal(0, cold.CacheHits);
        Assert.Single(cold.Findings, static finding => finding.RuleId == CollectionExpressionSpread);

        scratch.Write("B.cs", BBox);
        var truth = Run(scratch, project, false);
        Assert.DoesNotContain(truth.Findings, static finding => finding.RuleId == CollectionExpressionSpread);

        var warm = Run(scratch, project, true);

        // ⚠ The instrument: A.cs's per-file half was served, so this was a warm run.
        Assert.True(warm.CacheHits > 0, "nothing came from the cache, so this run proves nothing about it");
        Assert.DoesNotContain(warm.Findings, static finding => finding.RuleId == CollectionExpressionSpread);
        Assert.Single(warm.Findings, static finding => finding.RuleId == EmptyCatch);
        Assert.Equal(Describe(truth), Describe(warm));

        // And back: the finding returns to the file that never changed.
        scratch.Write("B.cs", BList);
        var restored = Run(scratch, project, true);
        Assert.Single(restored.Findings, static finding => finding.RuleId == CollectionExpressionSpread);
        Assert.Equal(Describe(cold), Describe(restored));
    }

    /// <summary>Nothing changed anywhere: every half of every file is served, and nothing is recomputed.</summary>
    [Fact]
    public void AnUntouchedCompilation_IsServedWhole() {
        using var scratch = new Scratch();
        var project = scratch.Write("Probe.csproj", Project);
        scratch.Write("Directory.Build.props", "<Project />");
        scratch.Write("Directory.Build.targets", "<Project />");
        scratch.Write("A.cs", A);
        scratch.Write("B.cs", BList);

        var cold = Run(scratch, project, true);
        var warm = Run(scratch, project, true);
        Assert.Equal(0, warm.CacheMisses);
        Assert.Equal(cold.CacheMisses, warm.CacheHits);
        Assert.Equal(Describe(cold), Describe(warm));
    }

    static IncrementalOutcome Run(Scratch scratch, string project, bool useCache) {
        var cancellation = TestContext.Current.CancellationToken;
        var loaded = ProjectLoader.Load(
            new() {
                RepositoryRoot = scratch.Root, Mode = LoadMode.Workspace, ProjectPath = project, AllowFallback = false
            },
            cancellation
        );
        Assert.Equal(LoadMode.Workspace, loaded.Mode);
        var unit = Assert.Single(loaded.Units);
        var (options, fingerprint, _) = EditorConfigOptions.For(unit, scratch.Root);

        return IncrementalAnalysis.Run(
            unit,
            options,
            [],
            LoadMode.Workspace,
            scratch.Root,
            fingerprint,
            useCache,
            cancellation
        );
    }

    static string Describe(IncrementalOutcome outcome) =>
        string.Join(
            "\n",
            outcome.Findings
                .Select(static finding =>
                    $"{finding.RuleId} {Path.GetFileName(finding.Path)}:{finding.Line}:{finding.Column}"
                )
                .Order(StringComparer.Ordinal)
        );
}
