using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     An array initializer's collection element the author broke after its <c>[</c> keeps the break and keeps
///     its <c>[</c> beside the element before it (Nightly fuzz, case 15104748770501078810, origin
///     <c>pathological/nested-collection-in-generated-while.cs</c>).
/// </summary>
/// <remarks>
///     ⚠ The fill measured such an element by its flat draft (#444), which read the kept break after the
///     <c>[</c> as a space: 97 columns, which fitted the continuation line, so pass one moved the <c>[</c> down
///     and kept the collection broken anyway. Pass two read its own <c>]</c> line as certain and kept
///     <c>), [</c>, the oracle's answer for both inputs. A call broken after its <c>(</c> still goes back on one
///     line (the last row). Expected output is the oracle's, measured 2026-10-09 with <c>Testing ask</c>.
/// </remarks>
public sealed class CollectionBrokenAfterItsBracketNightlyTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Source = """
                          class C {
                              void M() {
                                  var a = new[] { Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), [
                          57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", SourceXYZ], (null ? "ss" : 1.5d), [] };
                                  var b = new[] { alpha, [
                          1, 2, 3], beta };
                                  var c = new[] { Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), [
                          57926, "sss", SourceXYZ], gamma };
                                  var d = new[] { Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), [57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", SourceXYZ], (null ? "ss" : 1.5d), [] };
                                  var e = new[] { Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), Compute(
                          57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", Source), gamma };
                              }
                          }
                          """;

    const string Oracle = """
                          class C {
                              void M() {
                                  var a = new[] {
                                      Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), [
                                          57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", SourceXYZ
                                      ],
                                      (null ? "ss" : 1.5d), []
                                  };
                                  var b = new[] {
                                      alpha, [
                                          1, 2, 3
                                      ],
                                      beta
                                  };
                                  var c = new[] {
                                      Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"), [
                                          57926, "sss", SourceXYZ
                                      ],
                                      gamma
                                  };
                                  var d = new[] {
                                      Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"),
                                      [57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", SourceXYZ],
                                      (null ? "ss" : 1.5d), []
                                  };
                                  var e = new[] {
                                      Resolve("sssssssssssssssssssssssssssss", "ssssssssssss", true, name9: x10 => "sssssssssssssssssss"),
                                      Compute(57926, "sssssssssssssssssssssss", "sssssssssss", true, "sssssssssssssssssssssssssss", Source), gamma
                                  };
                              }
                          }
                          """;

    [Fact]
    public void ACollectionBrokenAfterItsBracket_StaysBesideThePreviousElement() {
        var formatted = FormatWith(Source);
        Assert.Equal(Oracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    // ⚠ Broken only before its `]` (Nightly fuzz, case 5848915233203857901): the oracle breaks it after its `[`
    // too and keeps the `[` beside `Compute(…),`, last or not. Pass one drafted it flat and moved the `[` down;
    // pass two read the break after the `[` it had written and joined it back. Measured 2026-10-09 with
    // `Testing ask` on 25 rows (breaks before `]`, after `[`, between elements, none; five positions).
    const string ClosingSource = """
                                 class C {
                                     void M() {
                                         var a = new object[] { this.OrderBy.Where.Where(@"verbatim\path").Value, source?.Value?.Count, Compute(@"verbatim\path", "sssssssssssssssss", x4 => 92651, x5 => true), [3_000_000L, "ss", null, 67712, 1.5d
                                 ] };
                                         var b = new object[] { Compute(@"verbatim\path", "sssssssssssssssss", x4 => 92651, x5 => true), [3_000_000L, "ss", null, 67712, 1.5d
                                 ], beta };
                                     }
                                 }
                                 """;

    const string ClosingOracle = """
                                 class C {
                                     void M() {
                                         var a = new object[] {
                                             this.OrderBy.Where.Where(@"verbatim\path").Value, source?.Value?.Count,
                                             Compute(@"verbatim\path", "sssssssssssssssss", x4 => 92651, x5 => true), [
                                                 3_000_000L, "ss", null, 67712, 1.5d
                                             ]
                                         };
                                         var b = new object[] {
                                             Compute(@"verbatim\path", "sssssssssssssssss", x4 => 92651, x5 => true), [
                                                 3_000_000L, "ss", null, 67712, 1.5d
                                             ],
                                             beta
                                         };
                                     }
                                 }
                                 """;

    [Fact]
    public void ACollectionBrokenOnlyBeforeItsClosingBracket_StaysBesideThePreviousElement() {
        var formatted = FormatWith(ClosingSource);
        Assert.Equal(ClosingOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    // ⚠ Broken by a `//` comment inside it (Nightly fuzz, case 6430242752800476221, #599): the oracle keeps the
    // `[` beside the element before it, however short the collection; pass one drafted it flat and moved the `[`
    // down. A block comment breaks nothing (the last row). Measured 2026-10-10 with `Testing ask` on 24 rows
    // (a comment after the first, a middle and the last element, a short collection, a block comment; four
    // positions).
    const string CommentSource = """"
        class C {
            void M() {
                var v17 = new[] { """<x a="1"/>""", checked(true), $"n={Compute(value_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww)}", [1, // fuzz
        2], 9718, z };
                var v18 = new[] { """<x a="1"/>""", checked(true), $"n={Compute(value_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww)}", [1, // fuzz
        2] };
                var v21 = new[] { """<x a="1"/>""", checked(true), $"n={Compute(value_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww)}", ["sssssssssssss", /* fuzz */ true], 9718, z };
            }
        }
        """";

    const string CommentOracle = """"
        class C {
            void M() {
                var v17 = new[] {
                    """<x a="1"/>""", checked(true), $"n={Compute(value_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww)}", [
                        1, // fuzz
                        2
                    ],
                    9718, z
                };
                var v18 = new[] {
                    """<x a="1"/>""", checked(true), $"n={Compute(value_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww)}", [
                        1, // fuzz
                        2
                    ]
                };
                var v21 = new[] {
                    """<x a="1"/>""", checked(true), $"n={Compute(value_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww)}",
                    ["sssssssssssss", /* fuzz */ true], 9718, z
                };
            }
        }
        """";

    [Fact]
    public void ACollectionBrokenByALineComment_StaysBesideThePreviousElement() {
        var formatted = FormatWith(CommentSource);
        Assert.Equal(CommentOracle + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
