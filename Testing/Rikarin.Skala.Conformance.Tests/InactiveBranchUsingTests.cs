using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     ⚠ #637: <c>SK0210</c> may not remove a using that only an inactive preprocessor branch needs.
/// </summary>
/// <remarks>
///     A compilation is one configuration. The binlog is the Release build, so the code under
///     <c>#if DEBUG</c> is disabled text the binder never sees, <c>CS8019</c> calls the using unnecessary,
///     and the safety re-bind — which re-binds under the same symbols — agrees. The Debug build then fails
///     <c>CS0246</c>.
///     <para>
///         ⚠ Every "kept" case asserts its premise first: that the compiler, asked about the loaded
///         configuration alone, <em>does</em> call the using unnecessary. Without that, a test whose using
///         was simply used in the active code would pass with the fix reverted. And the "still removed"
///         cases outnumber the kept ones, because the cheap fix — keep every using in any file holding an
///         <c>#if</c> — passes every kept case and is the defect class this file exists to rule out.
///     </para>
/// </remarks>
public sealed class InactiveBranchUsingTests {
    // ---- kept: the inactive branch is the only user ------------------------------------------------

    /// <summary>The issue's own reproduction, bound as the Release binlog binds it.</summary>
    [Fact]
    public void AUsingNeededOnlyUnderAnUndefinedIfDebug_IsKept() {
        const string source = """
                              using System.Diagnostics;

                              namespace Repro;

                              public static class DebugOnly {
                                  public static string? Where() {
                              #if DEBUG
                                      return new StackTrace(1, true).ToString();
                              #else
                                      return null;
                              #endif
                                  }
                              }
                              """;

        AssertKept(source, [], "System.Diagnostics");
        Assert.Contains("using System.Diagnostics;", Pipeline(source, []).Text, StringComparison.Ordinal);
    }

    /// <summary>The other half of the same <c>#if</c>: bound as Debug, the <c>#else</c> is the user.</summary>
    [Fact]
    public void AUsingNeededOnlyUnderTheElseOfADefinedSymbol_IsKept() {
        const string source = """
                              using System.Text;

                              namespace P;

                              public static class C {
                                  public static string M() {
                              #if DEBUG
                                      return "debug";
                              #else
                                      return new StringBuilder().Append('r').ToString();
                              #endif
                                  }
                              }
                              """;

        AssertKept(source, ["DEBUG"], "System.Text");
        Assert.Contains("using System.Text;", Pipeline(source, ["DEBUG"]).Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ An <c>#elif</c> is taken only when every condition before it is false, so a configuration
    ///     that defines the first symbol does not reach it — the chain's earlier conditions are part of
    ///     what has to be enumerated.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("NET_A")]
    public void AUsingNeededOnlyUnderAnElif_IsKept(string defined) {
        var symbols = Symbols(defined);
        const string source = """
                              using System.Text;

                              namespace P;

                              public static class C {
                                  public static string M() {
                              #if NET_A
                                      return "a";
                              #elif NET_B
                                      return new StringBuilder().ToString();
                              #else
                                      return "other";
                              #endif
                                  }
                              }
                              """;

        AssertKept(source, symbols, "System.Text");
    }

    /// <summary>A branch under two <c>#if</c>s needs both symbols, and only the pair activates it.</summary>
    [Fact]
    public void AUsingNeededOnlyUnderANestedIf_IsKept() {
        const string source = """
                              using System.Text;

                              namespace P;

                              public static class C {
                                  public static string M() {
                              #if OUTER
                                      var a = "outer";
                              #if INNER
                                      return new StringBuilder(a).ToString();
                              #else
                                      return a;
                              #endif
                              #else
                                      return "neither";
                              #endif
                                  }
                              }
                              """;

        AssertKept(source, [], "System.Text");
        AssertKept(source, ["OUTER"], "System.Text");
    }

    /// <summary>
    ///     An alias is keyed by alias and target, and its only use under <c>#if</c> keeps it like any other.
    /// </summary>
    [Fact]
    public void AnAliasNeededOnlyUnderAnInactiveBranch_IsKept() {
        const string source = """
                              using Builder = System.Text.StringBuilder;

                              namespace P;

                              public static class C {
                                  public static string M() {
                              #if DEBUG
                                      return new Builder().ToString();
                              #else
                                      return "";
                              #endif
                                  }
                              }
                              """;

        AssertKept(source, [], "Builder=System.Text.StringBuilder");
    }

    /// <summary>
    ///     ⚠ Why the branch is re-bound rather than scanned for names: the disabled text never says
    ///     <c>Linq</c> or <c>Enumerable</c>. Only the binder knows <c>Where</c> is the namespace's.
    /// </summary>
    [Fact]
    public void AUsingNeededOnlyForAnExtensionMethodUnderAnInactiveBranch_IsKept() {
        const string source = """
                              using System.Linq;

                              namespace P;

                              public static class C {
                                  public static int M(int[] values) {
                              #if DEBUG
                                      return values.Where(static v => v > 0).Count();
                              #else
                                      return values.Length;
                              #endif
                                  }
                              }
                              """;

        AssertKept(source, [], "System.Linq");
    }

    /// <summary>
    ///     ⚠ A branch whose condition chain names more symbols than are enumerated is not checked, and an
    ///     unchecked branch might need the using — so the file loses nothing.
    /// </summary>
    [Fact]
    public void ABranchTooWideToEnumerate_RemovesNothingFromItsFile() {
        var condition = string.Join(
            " && ",
            Enumerable.Range(0, InactiveBranches.MaxSymbolsPerBranch + 1)
                .Select(static i => "S" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))
        );
        var source = "using System.Text;\n\nnamespace P;\n\npublic static class C {\n    public static int M() {\n#if "
            + condition
            + "\n        return Environment.ProcessorCount;\n#else\n        return 0;\n#endif\n    }\n}\n";

        Assert.Null(InactiveBranches.Variants(Parse(source, []), TestContext.Current.CancellationToken));
        AssertKept(source, [], "System.Text");
    }

    // ---- still removed: an #if is not a reason on its own ----------------------------------------

    /// <summary>
    ///     ⚠ The one that rules out the coarse fix. The inactive branch holds code, and none of it is the
    ///     namespace's — so the using goes, while a used one beside it stays.
    /// </summary>
    [Fact]
    public void AnInactiveBranchThatNeverNamesTheNamespace_StillLetsTheUsingGo() {
        const string source = """
                              using System;
                              using System.Text;

                              namespace P;

                              public static class C {
                                  public static void M() {
                              #if DEBUG
                                      Console.WriteLine("debug");
                              #else
                                      Console.WriteLine("release");
                              #endif
                                  }
                              }
                              """;

        // ⚠ The branch was re-bound — not skipped — and still let the using go.
        Assert.Single(InactiveBranches.Variants(Parse(source, []), TestContext.Current.CancellationToken)!);
        var unused = Unused(source, []);
        Assert.Equal(["System.Text"], unused);

        var arranged = Pipeline(source, []).Text;
        Assert.DoesNotContain("using System.Text;", arranged, StringComparison.Ordinal);
        Assert.Contains("using System;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The kept using and an unused one in the same file: the branch keeps exactly what it uses.
    /// </summary>
    [Fact]
    public void AnInactiveBranchKeepsWhatItUses_AndNothingElse() {
        const string source = """
                              using System.Diagnostics;
                              using System.Text;

                              namespace P;

                              public static class C {
                                  public static string? M() {
                              #if DEBUG
                                      return new StackTrace().ToString();
                              #else
                                      return null;
                              #endif
                                  }
                              }
                              """;

        Assert.Equal(["System.Diagnostics", "System.Text"], CompilerAnswer(source, []).Order(StringComparer.Ordinal));
        Assert.Equal(["System.Text"], Unused(source, []));

        var arranged = Pipeline(source, []).Text;
        Assert.Contains("using System.Diagnostics;", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Text;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The <c>#elif</c> chain of <see cref="AUsingNeededOnlyUnderAnElif_IsKept" />, with no branch
    ///     naming the namespace: every branch is re-bound, and none of them keeps it.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("NET_A")]
    public void AnElifChainThatNeverNamesTheNamespace_StillLetsTheUsingGo(string defined) {
        const string source = """
                              using System;
                              using System.Text;

                              namespace P;

                              public static class C {
                                  public static string M() {
                              #if NET_A
                                      return Environment.NewLine;
                              #elif NET_B
                                      return Environment.MachineName;
                              #else
                                      return Environment.UserName;
                              #endif
                                  }
                              }
                              """;

        var symbols = Symbols(defined);
        Assert.Equal(["System.Text"], Unused(source, symbols));
        Assert.NotEmpty(InactiveBranches.Variants(Parse(source, symbols), TestContext.Current.CancellationToken)!);
    }

    /// <summary><c>#if false</c> is built in no configuration, so it cannot need a using.</summary>
    [Fact]
    public void AnIfFalseBranch_IsDeadCode_AndDoesNotKeepTheUsing() {
        const string source = """
                              using System.Text;

                              namespace P;

                              public static class C {
                                  public static string M() {
                              #if false
                                      return new StringBuilder().ToString();
                              #endif
                                      return "";
                                  }
                              }
                              """;

        Assert.Equal(["System.Text"], Unused(source, []));
        Assert.DoesNotContain("using System.Text;", Pipeline(source, []).Text, StringComparison.Ordinal);
    }

    /// <summary>The <c>#else</c> of <c>#if true</c> is dead the same way.</summary>
    [Fact]
    public void TheElseOfIfTrue_IsDeadCode_AndDoesNotKeepTheUsing() {
        const string source = """
                              using System.Text;

                              namespace P;

                              public static class C {
                                  public static string M() {
                              #if true
                                      return "";
                              #else
                                      return new StringBuilder().ToString();
                              #endif
                                  }
                              }
                              """;

        Assert.Equal(["System.Text"], Unused(source, []));
    }

    /// <summary>
    ///     A symbol the file <c>#undef</c>s is undefined under every configuration, so its branch is dead
    ///     whatever the build defines.
    /// </summary>
    [Fact]
    public void ABranchUnderASymbolTheFileUndefines_IsDeadCode_AndDoesNotKeepTheUsing() {
        const string source = """
                              #undef DEBUG
                              using System.Diagnostics;

                              namespace P;

                              public static class C {
                                  public static string? M() {
                              #if DEBUG
                                      return new StackTrace().ToString();
                              #else
                                      return null;
                              #endif
                                  }
                              }
                              """;

        Assert.Equal(["System.Diagnostics"], Unused(source, ["DEBUG"]));
    }

    /// <summary>
    ///     An inactive branch holding only a comment costs nothing: no re-parse is produced for it.
    /// </summary>
    [Fact]
    public void AnInactiveBranchHoldingOnlyAComment_IsNotReparsed_AndTheUsingGoes() {
        const string source = """
                              using System.Text;

                              namespace P;

                              public static class C {
                              #if DEBUG
                                  // StringBuilder would go here.
                              #endif
                                  public static int M() => 0;
                              }
                              """;

        Assert.Empty(InactiveBranches.Variants(Parse(source, []), TestContext.Current.CancellationToken)!);
        Assert.Equal(["System.Text"], Unused(source, []));
    }

    /// <summary>
    ///     ⚠ A using that is <em>itself</em> inside an inactive <c>#if</c> is disabled text, not a
    ///     directive: it is left where it stands, the block around it is still sorted and still loses its
    ///     unused using, and the <c>#if</c>/<c>#endif</c> pair survives in balance.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The same block bound with <c>DEBUG</c> defined is not covered here, and it is not fine.</b>
    ///     The inner using is then a directive whose leading trivia is the <c>#if</c>, the outer
    ///     <c>using System.Collections;</c> carries the <c>#endif</c>, and sorting moves the
    ///     <c>#endif</c> above the <c>#if</c>: the safety re-bind catches it and the file comes back
    ///     <c>SK9098</c> — unchanged, but reported as a Skala bug. The oracle (cleanup profile) instead
    ///     removes the unused using inside the active <c>#if</c>, sorts the outer ones together above it and
    ///     leaves an empty <c>#if DEBUG</c>/<c>#endif</c>. Measured during #637 and left for its own issue: it
    ///     is a sorting defect in an active branch, not the inactive-branch question this file is about.
    /// </remarks>
    [Fact]
    public void AUsingInsideAnInactiveIf_IsLeftAlone_WhileTheBlockAroundItIsArranged() {
        string[] symbols = [];
        const string source = """
                              using System.Text;
                              #if DEBUG
                              using System.Diagnostics;
                              #endif
                              using System.Collections;

                              namespace P;

                              public static class C {
                                  public static Hashtable H() => new();
                              }
                              """;

        var result = Pipeline(source, symbols);
        var arranged = result.Text;
        Assert.Empty(result.Diagnostics.Select(static d => d.Id + " " + d.Message));
        Assert.Equal(1, arranged.Split("#if DEBUG").Length - 1);
        Assert.Equal(1, arranged.Split("#endif").Length - 1);
        Assert.DoesNotContain(
            CSharpSyntaxTree.ParseText(
                    arranged,
                    CSharpFormatter.ParseOptions.WithPreprocessorSymbols(symbols),
                    cancellationToken: TestContext.Current.CancellationToken
                )
                .GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error
        );

        Assert.Contains("using System.Collections;", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Text;", arranged, StringComparison.Ordinal);
        Assert.Contains("#if DEBUG\nusing System.Diagnostics;\n#endif", arranged, StringComparison.Ordinal);
    }

    // ---- harness -----------------------------------------------------------------------------------

    static string[] Symbols(string defined) => defined.Split(',', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    ///     Asserts the premise — the loaded configuration alone calls <paramref name="key" /> unnecessary
    ///     — and then that <see cref="UsingsRule.Unused" /> keeps it.
    /// </summary>
    static void AssertKept(string source, string[] symbols, string key) {
        Assert.Contains(key, CompilerAnswer(source, symbols));
        Assert.DoesNotContain(key, Unused(source, symbols));
    }

    /// <summary>
    ///     What <c>CS8019</c> says about the configuration that was loaded, and nothing else: the answer
    ///     <see cref="UsingsRule.Unused" /> gave before #637.
    /// </summary>
    static string[] CompilerAnswer(string source, string[] symbols) {
        var (model, tree) = Bind(source, symbols);
        var root = tree.GetRoot(TestContext.Current.CancellationToken);
        return [
            ..model.GetDiagnostics(null, TestContext.Current.CancellationToken)
                .Where(static d => d.Id == "CS8019")
                .Select(d => root.FindNode(d.Location.SourceSpan)
                        .FirstAncestorOrSelf<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>()!
                )
                .Select(static directive => directive.Alias is { } alias
                        ? alias.Name.Identifier.ValueText + "=" + directive.Name
                        : directive.Name!.ToString()
                )
        ];
    }

    static string[] Unused(string source, string[] symbols) {
        var (model, tree) = Bind(source, symbols);
        return [..UsingsRule.Unused(model, tree, TestContext.Current.CancellationToken).Order(StringComparer.Ordinal)];
    }

    static SyntaxTree Parse(string source, string[] symbols) =>
        CSharpSyntaxTree.ParseText(
            SourceText.From(source),
            CSharpFormatter.ParseOptions.WithPreprocessorSymbols(symbols),
            "/arrangement/Probe.cs",
            TestContext.Current.CancellationToken
        );

    static CSharpCompilation Compile(SyntaxTree tree) =>
        CSharpCompilation.Create(
            "probe",
            [tree],
            SharedFrameworkReferences.Value,
            new(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

    static (SemanticModel Model, SyntaxTree Tree) Bind(string source, string[] symbols) {
        var tree = Parse(source, symbols);
        return (Compile(tree).GetSemanticModel(tree), tree);
    }

    /// <summary>
    ///     One arrange-and-format pipeline run, with the removable set computed the way the product does.
    /// </summary>
    static PipelineResult Pipeline(string source, string[] symbols) {
        var tree = Parse(source, symbols);
        var compilation = Compile(tree);
        var options = OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs"))
            .Options;

        return ArrangementPipeline.Run(
            tree.FilePath,
            tree.GetText(TestContext.Current.CancellationToken),
            new(options),
            new(options),
            compilation,
            UsingsRule.Unused(compilation.GetSemanticModel(tree), tree, TestContext.Current.CancellationToken),
            cancellation: TestContext.Current.CancellationToken
        );
    }
}
