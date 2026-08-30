using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     The arrangement options at the values the corpus fixtures cannot reach.
/// </summary>
/// <remarks>
///     ⚠ A committed <c>.arranged.expected.cs</c> pins one configuration: the repository's. That is the
///     right evidence for "Skala reproduces Rider on this file", and it is *not* evidence that the key
///     is read — a fixture generated at the export's value agrees with any implementation that happens
///     to agree there, which is how six documentation-comment keys were promoted one morning and
///     demoted the same afternoon.
///     <para>
///         ⚠ The key-flip sweep is what closes that gap, and for a long time it could not reach here:
///         <c>SweepPlan</c> dropped every arrangement option by name, because the format-only profile it
///         flipped against is byte-identical whatever an arrangement key says. So the flipped half was
///         written down by hand, from the oracle, the way SK-DIV-0013's three rewrites are — each
///         assertion below records an observed <c>jb cleanupcode</c> 2025.2.6 output under
///         <see cref="Rikarin.Skala.Testing.OracleProfile.Cleanup" />, probed one key at a time with every
///         key of its family restated so that a later <c>.editorconfig</c> section cannot reset its
///         siblings to Roslyn's defaults.
///     </para>
///     <para>
///         ⚠ <b>The sweep does reach here now</b>, under that same profile — see
///         <c>ArrangementRoutingTests</c>. These assertions are deliberately kept rather than deleted,
///         and they are not the weaker instrument's leftovers: they are transcriptions a human checked,
///         and a run of the automated one that contradicts them is a disagreement worth reading before
///         either is believed. What has changed is that they are no longer the <em>only</em> evidence
///         for 15 % of the Tier A claim.
///     </para>
/// </remarks>
public sealed class ArrangementOptionTests {
    static string Arrange(string source, params (string Key, string Value)[] overrides) {
        const string path = "/arrangement/Option.cs";
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(text, CSharpFormatter.ParseOptions, path);
        var compilation = CSharpCompilation.Create(
            "option",
            [tree],
            SharedFrameworkReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var resolved = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Option.cs"),
            [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
        );

        Assert.True(resolved.ValueErrors.IsEmpty, string.Join("; ", resolved.ValueErrors));

        var result = Arranger.Arrange(
            path,
            text,
            new ArrangementOptions(resolved.Options),
            compilation,
            UsingsRule.Unused(compilation.GetSemanticModel(tree), tree)
        );

        Assert.NotEqual(ArrangementOutcome.Reverted, result.Outcome);
        return result.Text;
    }

    const string Members = """
                           namespace P;
                           public delegate void Notify();
                           public class C {
                               int _value;
                               int Number { get; set; }
                               event Notify? Changed;
                               public void M() {
                                   _value = 1;
                                   Number = 2;
                                   Helper();
                                   Changed += Nothing;
                               }
                               static void Nothing() { }
                               void Helper() { }
                           }
                           """;

    /// <summary>
    ///     ⚠ One key, one member kind. The oracle's diff for each of the four is a single line.
    /// </summary>
    [Theory]
    [InlineData("dotnet_style_qualification_for_field", "this._value = 1;", "this.Number")]
    [InlineData("dotnet_style_qualification_for_property", "this.Number = 2;", "this._value")]
    [InlineData("dotnet_style_qualification_for_method", "this.Helper();", "this._value")]
    [InlineData("dotnet_style_qualification_for_event", "this.Changed += Nothing;", "this._value")]
    public void Qualification_AddsThisForItsOwnKindOnly(string key, string added, string untouched) {
        var output = Arrange(Members, (key, "true:suggestion"));
        Assert.Contains(added, output, StringComparison.Ordinal);
        Assert.DoesNotContain(untouched, output, StringComparison.Ordinal);
    }

    /// <summary>⚠ The export's value, and the direction the committed fixture pins.</summary>
    [Fact]
    public void Qualification_RemovesThisAtTheExportsValue() =>
        Assert.DoesNotContain(
            "this.",
            Arrange(Members.Replace("_value = 1;", "this._value = 1;", StringComparison.Ordinal)),
            StringComparison.Ordinal
        );

    /// <summary>
    ///     ⚠ A static body has no <c>this</c>, so the adding direction must decline it. Without this the
    ///     rule writes <c>this.Shared</c> into a static method and the file stops compiling — which the
    ///     re-bind layer would catch and revert, silently costing every other rewrite in the file.
    /// </summary>
    [Fact]
    public void Qualification_RefusesWhereThisIsNotLegal() {
        const string source = """
                              namespace P;
                              public class C {
                                  int _value;
                                  static int _shared;
                                  static void Static() { _shared = 1; }
                                  void Local() { void Inner() { } Inner(); _value = 2; }
                              }
                              """;

        var output = Arrange(source, ("dotnet_style_qualification_for_field", "true:suggestion"));
        Assert.Contains("_shared = 1;", output, StringComparison.Ordinal);
        Assert.DoesNotContain("this._shared", output, StringComparison.Ordinal);

        // ⚠ A local function is an IMethodSymbol whose ContainingType is the enclosing type, so the
        // obvious member test writes `this.Inner()` — which does not compile.
        Assert.DoesNotContain("this.Inner", output, StringComparison.Ordinal);
        Assert.Contains("this._value = 2;", output, StringComparison.Ordinal);
    }

    const string Parentheses = """
                               namespace P;
                               public class C {
                                   public int Arithmetic(int a, int b, int c) => a + (b * c);
                                   public bool Relational(int a, int b, int c, int d) => (a > b) == (c > d);
                                   public bool Other(bool a, bool b, bool c) => a || (b && c);
                                   public bool Mixed(int a, int b, int c) => (a + b) > c;
                                   public bool NoParent(bool a, bool b) => (a && b);
                               }
                               """;

    /// <summary>
    ///     ⚠ Each key holds the parentheses of its own precedence kind and no other's. Measured with all
    ///     three restated: <c>a + (b * c)</c> survives at arithmetic <c>always_for_clarity</c> while the
    ///     relational and the <c>&amp;&amp;</c> case are untouched by it.
    /// </summary>
    [Theory]
    [InlineData("dotnet_style_parentheses_in_arithmetic_binary_operators", "a + (b * c)")]
    [InlineData("dotnet_style_parentheses_in_relational_binary_operators", "(a > b) == (c > d)")]
    public void Parentheses_AlwaysForClarity_KeepsItsOwnKind(string key, string kept) {
        var output = Arrange(Parentheses, (key, "always_for_clarity:none"));
        Assert.Contains(kept, output, StringComparison.Ordinal);

        // ⚠ Same category on both sides is the whole rule: a parenthesised arithmetic operand of a
        // *relational* operator has no key and goes at every combination of the three.
        Assert.Contains("=> a + b > c;", output, StringComparison.Ordinal);
    }

    /// <summary>⚠ The export writes <c>always_for_clarity</c> here, so this is the flipped half.</summary>
    [Fact]
    public void Parentheses_Other_NeverIfUnnecessary_RemovesTheLogicalOnes() {
        var output = Arrange(
            Parentheses,
            ("dotnet_style_parentheses_in_other_binary_operators", "never_if_unnecessary:none")
        );

        Assert.Contains("=> a || b && c;", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The case that keying on the operand alone gets wrong. <c>&amp;&amp;</c> is
    ///     <c>always_for_clarity</c> and the oracle still removes these parentheses, because the parent is
    ///     not a binary operator at all.
    /// </summary>
    [Fact]
    public void Parentheses_AreRemovedWhenTheParentIsNotABinaryOperator() =>
        Assert.Contains("=> a && b;", Arrange(Parentheses), StringComparison.Ordinal);

    const string Nested = """
                          namespace P {
                              using System.Text;
                              using Culture = System.Globalization.CultureInfo;
                              public class C {
                                  public string M(int v) {
                                      var b = new StringBuilder();
                                      b.Append(v.ToString(Culture.InvariantCulture));
                                      return b.ToString();
                                  }
                              }
                          }
                          """;

    /// <summary>
    ///     ⚠ The export's direction: a plain directive written at nested scope is hoisted above the
    ///     namespace, and an alias is not. Both halves are the oracle's.
    /// </summary>
    [Fact]
    public void UsingPlacement_HoistsPlainDirectivesAndLeavesAliases() {
        var output = Arrange(Nested);
        var usingSystemText = output.IndexOf("using System.Text;", StringComparison.Ordinal);
        var namespaceLine = output.IndexOf("namespace P", StringComparison.Ordinal);
        var alias = output.IndexOf("using Culture =", StringComparison.Ordinal);

        Assert.True(usingSystemText >= 0 && namespaceLine >= 0 && alias >= 0, output);
        Assert.True(usingSystemText < namespaceLine, "the plain directive was not hoisted:\n" + output);
        Assert.True(alias > namespaceLine, "the alias was hoisted, and the oracle leaves it:\n" + output);
    }

    /// <summary>⚠ The other direction, which the export never asks for and the oracle performs.</summary>
    [Fact]
    public void UsingPlacement_InsideNamespace_PushesTheBlockDown() {
        const string source = """
                              using System.Text;

                              namespace P;

                              public class C {
                                  public string M() => new StringBuilder().ToString();
                              }
                              """;

        var output = Arrange(source, ("csharp_using_directive_placement", "inside_namespace:silent"));
        Assert.True(
            output.IndexOf("namespace P", StringComparison.Ordinal)
            < output.IndexOf("using System.Text;", StringComparison.Ordinal),
            "the block was not pushed inside:\n" + output
        );
    }

    const string Groups = """
                          using Zeta.Support;
                          using System.Text;

                          namespace P {
                              public class C {
                                  public string M() {
                                      var b = new StringBuilder();
                                      b.Append(Helper.Twice(1));
                                      return b.ToString();
                                  }
                              }
                          }

                          namespace Zeta.Support {
                              public static class Helper {
                                  public static int Twice(int v) => v * 2;
                              }
                          }
                          """;

    /// <summary>
    ///     ⚠ Grouped by first namespace segment, one blank line, and only between groups.
    /// </summary>
    [Fact]
    public void SeparateImportGroups_WritesOneBlankLineBetweenFirstSegments() {
        var output = Arrange(Groups, ("dotnet_separate_import_directive_groups", "true"));
        Assert.Contains(
            "using System.Text;\n\nusing Zeta.Support;",
            output.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     ⚠ And takes one back out at the export's <c>false</c>. SK-DIV-0074: the oracle does this under
    ///     <c>CSReformatCode</c> alone and Skala's formatter does not read the key at all, so the
    ///     arranger is the only place it happens — which is why the corpus fixture cannot carry the
    ///     removal direction and this test does.
    /// </summary>
    [Fact]
    public void SeparateImportGroups_RemovesABlankLineAtTheExportsValue() {
        var output = Arrange(
            Groups.Replace("using Zeta.Support;\n", "using Zeta.Support;\n\n", StringComparison.Ordinal)
        )
                .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("using System.Text;\nusing Zeta.Support;", output, StringComparison.Ordinal);
    }

    const string Aliases = """
                           using Builder = System.Text.StringBuilder;
                           using Numbers = System.Collections.Generic.List<int>;
                           using StringBuilder = System.Text.StringBuilder;

                           namespace P;

                           public class C {
                               public string M() => new Builder().ToString();
                           }
                           """;

    /// <summary>
    ///     ⚠ The four-way table, measured. An unused *trivial* alias — one whose name is the aliased
    ///     type's own name — goes at every combination; an unused non-trivial one goes only at the
    ///     export's pair, which is what makes each of the two keys observable on its own.
    /// </summary>
    [Theory]
    [InlineData("false", "true", false)]
    [InlineData("true", "true", true)]
    [InlineData("false", "false", true)]
    [InlineData("true", "false", true)]
    public void Aliases_UnusedNonTrivialGoesOnlyAtTheExportsPair(string keep, string removeOnlyUnused, bool survives) {
        var output = Arrange(
            Aliases,
            ("resharper_csharp_keep_nontrivial_alias", keep),
            ("resharper_remove_only_unused_aliases", removeOnlyUnused)
        );

        Assert.Contains("using Builder =", output, StringComparison.Ordinal);
        Assert.DoesNotContain("using StringBuilder =", output, StringComparison.Ordinal);
        Assert.Equal(survives, output.Contains("using Numbers =", StringComparison.Ordinal));
    }

    const string Discards = """
                            namespace P;

                            public class C {
                                public void Deconstruct(out int first, out int second) {
                                    first = 1;
                                    second = 2;
                                }

                                public void Bare() {
                                    Deconstruct(out var kept, out _);
                                    System.Console.WriteLine(kept);
                                }

                                public void Explicit() {
                                    Deconstruct(out var kept, out var _);
                                    System.Console.WriteLine(kept);
                                }
                            }
                            """;

    /// <summary>
    ///     ⚠ The space between <c>var</c> and <c>_</c> is the whole of this test.
    /// </summary>
    /// <remarks>
    ///     ⚠ A <c>DeclarationExpression</c> built from two nodes that carry no trivia prints
    ///     <c>var_</c>, which is one identifier and not a declaration. The re-bind reported
    ///     <c>CS0103: The name 'var_' does not exist in the current context</c>, safety layer 3 reverted
    ///     the document whole, and the arranger's answer at this key's <c>true</c> was therefore the
    ///     input byte for byte — every other rule's work on the file lost with it. The key-flip sweep
    ///     read that as <c>DIVERGENT</c> and could not say why, because a reverted file looks exactly
    ///     like a rule that declined. The <c>Arrange</c> helper above already asserts
    ///     <c>NotEqual(Reverted)</c>; nothing asked it at this value until now, which is the gap rather
    ///     than the assertion.
    /// </remarks>
    [Fact]
    public void Discard_ExplicitDeclaration_WritesTheSpaceAfterVar() {
        var output = Arrange(Discards, ("resharper_csharp_prefer_explicit_discard_declaration", "true"));

        Assert.Contains("out var _", output, StringComparison.Ordinal);
        Assert.DoesNotContain("var_", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ The export's <c>false</c>, which *removes* an existing <c>var _</c> rather than merely
    ///     declining to write one — the second row of the table in <c>DiscardDeclarationRule</c>'s own
    ///     remarks, and the row that answers the key.
    /// </summary>
    [Fact]
    public void Discard_AtTheExportsValue_StripsAnExistingVar() {
        var output = Arrange(Discards);

        Assert.Contains("out _", output, StringComparison.Ordinal);
        Assert.DoesNotContain("out var _", output, StringComparison.Ordinal);
    }
}
