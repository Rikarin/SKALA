using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #583, SK-DIV-0127: a local one column past the margin breaks between its type and its name. Every expected
///     string is <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class LocalOneOverTypeNameIssue583Tests {
    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    /// <summary>
    ///     #583: at a 121-column line a local breaks between its type and its name by the two widths: a 24-column
    ///     <c>Func&lt;…&gt;</c>'s gap up to the head at 42 and its <c>=</c> from 43, a 32-column type's up to 93, a
    ///     22-column one's for a one-letter name only, and a 36-column type's always.
    /// </summary>
    [Fact]
    public void APlainValueOneColumnOver() {
        Agrees(
            $$"""
              class C {
                  void M() {
                      Func<TTTTTTTTTTTTTTTTTT> ggggggg = {{R('w', 77)}};
                      Func<TTTTTTTTTTTTTTTTTT> gggggggg = {{R('w', 76)}};
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> {{R('g', 50)}} = wwwwwwwwwwwwwwwwwwwwwwwwww;
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> {{R('g', 51)}} = wwwwwwwwwwwwwwwwwwwwwwwww;
                      Func<TTTTTTTTTTTTTTTT> g = {{R('w', 85)}};
                      Func<TTTTTTTTTTTTTTTT> gg = {{R('w', 84)}};
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> {{R('g', 63)}} = wwwwwwwww;
                  }
              }
              """,
            """
            class C {
                void M() {
                    Func<TTTTTTTTTTTTTTTTTT>
                        ggggggg = wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww;
                    Func<TTTTTTTTTTTTTTTTTT> gggggggg =
                        wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww;
                    Func<TTTTTTTTTTTTTTTTTTTTTTTTTT>
                        gggggggggggggggggggggggggggggggggggggggggggggggggg = wwwwwwwwwwwwwwwwwwwwwwwwww;
                    Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> ggggggggggggggggggggggggggggggggggggggggggggggggggg =
                        wwwwwwwwwwwwwwwwwwwwwwwww;
                    Func<TTTTTTTTTTTTTTTT>
                        g = wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww;
                    Func<TTTTTTTTTTTTTTTT> gg =
                        wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww;
                    Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT>
                        ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg = wwwwwwwww;
                }
            }
            """
        );
    }

    /// <summary>#583: at 122 columns the same locals break their <c>=</c>.</summary>
    [Fact]
    public void OneColumnFurther_TheEqualsBreaks() {
        Agrees(
            $$"""
              class C {
                  void M() {
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> ggggggggggggg = {{R('w', 60)}};
                      Func<TTTTTTTTTTTTTTTTTT> ggggggg = {{R('w', 78)}};
                  }
              }
              """,
            """
            class C {
                void M() {
                    Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> ggggggggggggg =
                        wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww;
                    Func<TTTTTTTTTTTTTTTTTT> ggggggg =
                        wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww;
                }
            }
            """
        );
    }

    /// <summary>
    ///     #583: a lambda keeps the arrow up to the head at 48 and chops its parameters at 49 (#558, #572), and gives
    ///     the line to the type/name gap after that wherever the widths allow it; a 26-column type does not, and the
    ///     <c>=</c> breaks.
    /// </summary>
    [Fact]
    public void ALambdaOneColumnOver() {
        Agrees(
            $$"""
              class C {
                  void M() {
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = ({{R('P', 54)}} p0) => vvvvvvvv;
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> ff = ({{R('P', 53)}} p0) => vvvvvvvv;
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> fff = ({{R('P', 52)}} p0) => vvvvvvvv;
                      Func<{{R('T', 36)}}> fffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvvvvvvvvvvvvvvv;
                      Func<TTTTTTTTTTTTTTTTTTTT> ffffffffffffffffff = ({{R('P', 47)}} p0) => vvvvvvvv;
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = ({{R('P', 54)}} p0) =>
                          vvvvvvvv;
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> ff = (
                          PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0
                      ) => vvvvvvvv;
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT>
                          fff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvvvvvvv;
                      Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT>
                          fffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvvvvvvvvvvvvvvv;
                      Func<TTTTTTTTTTTTTTTTTTTT> ffffffffffffffffff =
                          (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvvvvvvv;
                  }
              }
              """
        );
    }

    /// <summary>#583: the arrow's reach grows with the indent: 54 at indent 12, the gap from 55.</summary>
    [Fact]
    public void ALambdaOneColumnOver_Indented() {
        Agrees(
            $$"""
              class C {
                  void M() {
                      {
                          Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> fffffff = ({{R('P', 40)}} p0) => vvvvvvvvvvvvvvvv;
                          Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> ffffffff = ({{R('P', 39)}} p0) => vvvvvvvvvvvvvvvv;
                          Func<{{R('T', 36)}}> fffffffffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvvvvvvv;
                      }
                  }
              }
              """,
            """
            class C {
                void M() {
                    {
                        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> fffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) =>
                            vvvvvvvvvvvvvvvv;
                        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT>
                            ffffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvvvvvvvvvvvvvvv;
                        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT>
                            fffffffffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvvvvvvv;
                    }
                }
            }
            """
        );
    }
}
