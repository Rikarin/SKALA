using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
/// <c>@formatter:off</c> binds arrangement, and the oracle's cleanup profile says it does not.
/// </summary>
/// <remarks>
/// ⚠ SK-DIV-0016, and the one divergence in this file that is a divergence *from a measurement*
/// rather than from an absence. <c>jb cleanupcode</c>'s <c>CSReformatCode</c> honours the tags —
/// pinned by <c>constructs/arrangement/formatter-tags/*.expected.cs</c> — and its cleanup profile
/// ignores them completely, pinned by the <c>.arranged.expected.cs</c> beside each of those, where
/// the region comes back with its bodies folded into expression bodies and its trailing comma gone.
/// Skala respects them under both, because the user's expectation is the requirement and doc 00's
/// non-negotiable 9 says the reference tool is a test subject rather than a specification.
/// <para>
/// ⚠ Every assertion here was verified by mutation — <c>GuardedRewriter.Visit</c> made to return
/// <c>base.Visit(node)</c> unconditionally, and <c>Arranger</c>'s <c>PreservesAll</c> check removed
/// — and each one was watched to fail before the guard was put back.
/// </para>
/// </remarks>
public sealed class ArrangementTagTests {
    static string Arrange(string source, bool tagsEnabled = true) {
        const string path = "/arrangement/Tagged.cs";
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, path);
        var compilation = CSharpCompilation.Create(
            "tagged",
            [tree],
            SharedFrameworkReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Probe.cs"),
            tagsEnabled ? null : [new KeyValuePair<string, string>("resharper_formatter_tags_enabled", "false")]
        )
                .Options;

        var result = Arranger.Arrange(path, text, new ArrangementOptions(options), compilation);
        Assert.NotEqual(ArrangementOutcome.Reverted, result.Outcome);
        return result.Text;
    }

    /// <summary>The user's case: a hand-aligned table that must survive verbatim.</summary>
    [Fact]
    public void TheRegion_SurvivesByteForByte_AndTheCodeOutsideIsStillArranged() {
        const string region = """
                                  // @formatter:off
                                  public  int  Old( )   { return 1; }
                                  public List<int> Made()  { return new List<int>(); }
                                  private System.Int32 Width() { return 3; }
                                  // @formatter:on
                              """;

        var arranged = Arrange(
            $$"""
              using System.Collections.Generic;

              public class C {
              {{region}}
                  public int New() { return 2; }
              }
              """
        );

        Assert.Contains(region, arranged, StringComparison.Ordinal);
        Assert.Contains("=> 2;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ The straddling-node decision, asserted rather than described: the signature is outside the
    /// region and the body is inside it, and the <em>whole node</em> is skipped — <c>System.Int32</c>
    /// is not reduced to <c>int</c> and the redundant <c>private</c> is not removed, even though both
    /// are outside the tags.
    /// </summary>
    [Fact]
    public void ANodeStraddlingATag_IsSkippedWhole() {
        var arranged = Arrange(
            """
            public class C {
                private System.Int32 Straddles() {
                    // @formatter:off
                    return 3;
                }
                // @formatter:on
            }
            """
        );

        Assert.Contains("private System.Int32 Straddles() {", arranged, StringComparison.Ordinal);
        Assert.Contains("return 3;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ The case the span tests cannot see on their own. The region lies <em>inside</em> the method,
    /// so the method neither straddles a tag nor sits within one — and body-style arrangement would
    /// still fold the block into <c>=&gt;</c> and take both tags with it. What stops it is
    /// <see cref="FormatterTagGuard.Preserves"/>, which asks whether the protected bytes are still
    /// there rather than where they were.
    /// </summary>
    [Fact]
    public void AMethodWhoseWholeBodyIsInsideTheRegion_KeepsItsBlockBody() {
        var arranged = Arrange(
            """
            using System.Collections.Generic;

            public class C {
                public List<int> Contains() {
                    // @formatter:off
                    return new List<int>();
                    // @formatter:on
                }
            }
            """
        );

        Assert.Contains("return new List<int>();", arranged, StringComparison.Ordinal);
        Assert.DoesNotContain("Contains() =>", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ A class that <em>contains</em> a region is not frozen by it, or one tag anywhere would
    /// freeze the class, then the namespace, then the file.
    /// </summary>
    [Fact]
    public void AClassContainingARegion_IsStillArrangedOutsideIt() {
        var arranged = Arrange(
            """
            public class C {
                // @formatter:off
                public  int  Old( )   { return 1; }
                // @formatter:on

                public int New() { return 2; }
            }
            """
        );

        Assert.Contains("public  int  Old( )   { return 1; }", arranged, StringComparison.Ordinal);
        Assert.Contains("=> 2;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ <see cref="UsingsRule"/> rebuilds the using block by hand instead of through a rewriter, so
    /// it never passes through <see cref="GuardedRewriter"/> at all. It is what
    /// <see cref="FormatterTagGuard.PreservesAll"/> exists for, and it is why that backstop is not
    /// belt-and-braces.
    /// </summary>
    [Fact]
    public void TheUsingBlock_IsNotReorderedAcrossATag() {
        const string block = """
                             // @formatter:off
                             using System.Text;
                             using System;
                             // @formatter:on
                             """;

        var arranged = Arrange(
            $$"""
              {{block}}

              public class C {
                  public string M() { return string.Join(",", new System.Text.StringBuilder().ToString()); }
              }
              """
        );

        Assert.Contains(block, arranged, StringComparison.Ordinal);
    }

    /// <summary>⚠ An unterminated <c>off</c> runs to the end of the file, as it does in the formatter.</summary>
    [Fact]
    public void AnUnterminatedOff_ProtectsTheRestOfTheFile() {
        const string tail = """
                                // @formatter:off
                                public  int  After( )   { return 1; }
                            """;

        var arranged = Arrange(
            $$"""
              public class C {
                  public int Before() { return 0; }
              {{tail}}
              }
              """
        );

        Assert.Contains(tail, arranged, StringComparison.Ordinal);
        Assert.Contains("=> 0;", arranged, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠ Turning the tags off turns the guard off, which is what makes every assertion above a
    /// statement about the tag rather than about arrangement declining to fire.
    /// </summary>
    [Fact]
    public void WithTheTagsDisabled_TheSameRegionIsArranged() {
        const string source = """
                              public class C {
                                  // @formatter:off
                                  public int Old() { return 1; }
                                  // @formatter:on
                              }
                              """;

        Assert.Contains("=> 1;", Arrange(source, tagsEnabled: false), StringComparison.Ordinal);
        Assert.DoesNotContain("=> 1;", Arrange(source), StringComparison.Ordinal);
    }
}
