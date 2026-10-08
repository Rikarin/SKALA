using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #447, SK-DIV-0003: an interpolated raw string literal is shifted by
///     <c>skala_indent_raw_literal_string</c> exactly as a plain one is. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the source under <c>SkalaFormatOnly</c>, at the value
///     each test names, and every test asserts that a second pass changes nothing.
/// </summary>
public sealed class InterpolatedRawLiteralIssue447Tests {
    const string AlignSource = """"
                               class RawProbe {
                                   void M(int x) {
                                       var plain = """
                                                       Hello
                                                       World
                                                       """;
                                       var interp = $"""
                                                       Hello {x}
                                                       World
                                                       """;
                                       var interp2 = $$"""
                                                       Hello {{x}}
                                                       World
                                                       """;
                                       var hole = $"""
                                                       Hello {
                                                           x
                                                       } there
                                                       World
                                                       """;
                                       var nested = $"""
                                                       Outer {$"""
                                                                   inner {x}
                                                                   """}
                                                       World
                                                       """;
                                       var deeper = $"""
                                         Hello {x}
                                         World
                                         """;
                                       Call($"""
                                                       Arg {x}
                                                       """);
                                       var verbatim = $@"
                                                       Hello {x}
                                                       ";
                                   }

                                   void Call(string s) { }
                               }
                               """"
        + "\n";

    const string AlignOracle = """"
                               class RawProbe {
                                   void M(int x) {
                                       var plain = """
                                                   Hello
                                                   World
                                                   """;
                                       var interp = $"""
                                                     Hello {x}
                                                     World
                                                     """;
                                       var interp2 = $$"""
                                                       Hello {{x}}
                                                       World
                                                       """;
                                       var hole = $"""
                                                   Hello {
                                                       x
                                                   } there
                                                   World
                                                   """;
                                       var nested = $"""
                                                     Outer {$"""
                                                             inner {x}
                                                             """}
                                                     World
                                                     """;
                                       var deeper = $"""
                                                     Hello {x}
                                                     World
                                                     """;
                                       Call(
                                           $"""
                                            Arg {x}
                                            """
                                       );
                                       var verbatim = $@"
                                                       Hello {x}
                                                       ";
                                   }

                                   void Call(string s) { }
                               }
                               """"
        + "\n";

    const string IndentOracle = """"
                                class RawProbe {
                                    void M(int x) {
                                        var plain = """
                                            Hello
                                            World
                                            """;
                                        var interp = $"""
                                            Hello {x}
                                            World
                                            """;
                                        var interp2 = $$"""
                                            Hello {{x}}
                                            World
                                            """;
                                        var hole = $"""
                                            Hello {
                                                x
                                            } there
                                            World
                                            """;
                                        var nested = $"""
                                            Outer {$"""
                                                inner {x}
                                                """}
                                            World
                                            """;
                                        var deeper = $"""
                                            Hello {x}
                                            World
                                            """;
                                        Call(
                                            $"""
                                            Arg {x}
                                            """
                                        );
                                        var verbatim = $@"
                                                        Hello {x}
                                                        ";
                                    }

                                    void Call(string s) { }
                                }
                                """"
        + "\n";

    const string ChoppedSource = """"
                                 class R {
                                     void M(int x) {
                                         Call("""
                                                         Arg
                                                         """);
                                         Call($"""
                                                         Arg {x}
                                                         """);
                                         Call(1,
                                             """
                                                         Arg
                                                         """);
                                     }

                                     void Call(string s) { }
                                     void Call(int a, string s) { }
                                 }
                                 """"
        + "\n";

    const string ChoppedIndentOracle = """"
                                       class R {
                                           void M(int x) {
                                               Call(
                                                   """
                                                   Arg
                                                   """
                                               );
                                               Call(
                                                   $"""
                                                   Arg {x}
                                                   """
                                               );
                                               Call(
                                                   1,
                                                   """
                                                   Arg
                                                   """
                                               );
                                           }

                                           void Call(string s) { }
                                           void Call(int a, string s) { }
                                       }
                                       """"
        + "\n";

    const string NestedIndentOracle = """"
                                      class R {
                                          void M(int x) {
                                              Call(
                                                  1,
                                                  Inner(
                                                      2,
                                                      """
                                                      Arg
                                                      """
                                                  )
                                              );
                                              Call(
                                                  1,
                                                  Inner(
                                                      2,
                                                      $"""
                                                      Arg {x}
                                                      """
                                                  )
                                              );
                                              if (x > 0) {
                                                  Call(
                                                      """
                                                      Arg
                                                      """
                                                  );
                                              }
                                          }

                                          string Inner(int a, string s) => s;
                                          void Call(string s) { }
                                          void Call(int a, string s) { }
                                      }
                                      """"
        + "\n";

    /// <summary>
    ///     At the export's <c>align</c>: a <c>$"""</c> literal lands on the column of its <c>"""</c> — not of
    ///     its <c>$</c> — so <c>$$"""</c> written at 24 stays; a nested <c>$"""</c> anchors on its own quotes
    ///     after its line moved with the outer literal; and <c>$@"…"</c> is untouched.
    /// </summary>
    /// <remarks>
    ///     ⚠ The expected text is the oracle's <em>second</em> pass, which a third leaves unchanged. Its first
    ///     pass differs on exactly two lines — the ones that begin inside the <c>Hello {</c> hole, which it
    ///     leaves where they were and then moves with <c>Hello {</c> when given its own output back. A hole
    ///     line moves with the line its hole opens on.
    /// </remarks>
    [Fact]
    public void AtAlign_EachLiteralLandsOnItsOwnQuotes_AndHoleLinesMoveWithTheirOpeningLine() =>
        Agrees(AlignSource, AlignOracle, "align");

    /// <summary>
    ///     At <c>indent</c>: the opening line's indentation plus one level, measured on the line where it
    ///     lands. Again the oracle's second pass and fixed point; its first measured a nested literal's
    ///     opening line where it was written (24, so 28) and left the hole lines.
    /// </summary>
    [Fact]
    public void AtIndent_TheOpeningLinePlusALevel() => Agrees(AlignSource, IndentOracle, "indent");

    /// <summary>
    ///     ⚠ At <c>indent</c>, a literal whose quotes start their line lands under its own quotes, with no
    ///     level added — plain and interpolated alike, at 12 and (given back) at 16.
    /// </summary>
    [Fact]
    public void AtIndent_ALiteralStartingItsLine_TakesNoExtraLevel() {
        Agrees(ChoppedSource, ChoppedIndentOracle, "indent");
        Agrees(NestedIndentOracle, NestedIndentOracle, "indent");
    }

    /// <summary>
    ///     ⚠ The premise the shift stands on, pinned: the safety net compares an interpolated raw literal's
    ///     text by its <em>stripped</em> value, so moving the closing delimiter alone — which changes every
    ///     content line's value — is caught, while moving every line together is not.
    /// </summary>
    [Fact]
    public void TheSafetyNet_CatchesAClosingDelimiterMovedWithoutItsContent() {
        const string before = "class C { void M(int x) { var s = $\"\"\"\n        a {x}\n        \"\"\"; } }\n";
        const string together = "class C { void M(int x) { var s = $\"\"\"\n    a {x}\n    \"\"\"; } }\n";
        const string closerOnly = "class C { void M(int x) { var s = $\"\"\"\n        a {x}\n    \"\"\"; } }\n";

        Assert.Null(
            TokenEquivalence.Compare(SourceText.From(before), SourceText.From(together), CSharpFormatter.ParseOptions)
        );
        Assert.NotNull(
            TokenEquivalence.Compare(SourceText.From(before), SourceText.From(closerOnly), CSharpFormatter.ParseOptions)
        );
    }

    static void Agrees(string source, string expected, string value) {
        var once = Format(source, value);
        Assert.Equal(expected, once);
        Assert.Equal(once, Format(once, value));
    }

    static string Format(string source, string value) {
        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
            [new KeyValuePair<string, string>("skala_indent_raw_literal_string", value)]
        ).Options;
        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options)
            .Formatted.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
