using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #464, SK-DIV-0090 and SK-DIV-0107: a braced initializer's elements and a switch expression's arms
///     take <c>skala_continuous_indent_multiplier</c> indent widths, with the <c>}</c> on the opener's level, and
///     one width under <c>skala_use_continuous_indent_inside_initializer_braces = false</c>. Every expected string
///     is <c>jb cleanupcode</c>'s own output for the input under the export with the overrides shown, and each
///     test asserts the second pass too. The export's multiplier of 1 cannot tell the two readings apart, which is
///     why no committed fixture does.
/// </summary>
public sealed class BracedContinuationMultiplierIssue464Tests {
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

    const string Source = """
                          class C {
                              void M() {
                                  var list = new List<string> {
                                      "an element long enough to keep the initializer broken across lines",
                                      "a second element long enough to keep the initializer broken too"
                                  };
                                  var obj = new T {
                                      Alpha = "an element long enough to keep the initializer broken across lines",
                                      Bravo = "a second element long enough to keep the initializer broken too"
                                  };
                                  var s = a switch {
                                      _ => 1
                                  };
                              }
                          }
                          """;

    [Fact]
    public void AtTwo_TheContentsTakeTwoWidths() =>
        Agrees(
            Source,
            """
            class C {
                void M() {
                    var list = new List<string> {
                            "an element long enough to keep the initializer broken across lines",
                            "a second element long enough to keep the initializer broken too"
                    };
                    var obj = new T {
                            Alpha = "an element long enough to keep the initializer broken across lines",
                            Bravo = "a second element long enough to keep the initializer broken too"
                    };
                    var s = a switch {
                            _ => 1
                    };
                }
            }
            """,
            ("skala_continuous_indent_multiplier", "2")
        );

    [Fact]
    public void AtThree_TheContentsTakeThreeWidths() =>
        Agrees(
            Source,
            """
            class C {
                void M() {
                    var list = new List<string> {
                                "an element long enough to keep the initializer broken across lines",
                                "a second element long enough to keep the initializer broken too"
                    };
                    var obj = new T {
                                Alpha = "an element long enough to keep the initializer broken across lines",
                                Bravo = "a second element long enough to keep the initializer broken too"
                    };
                    var s = a switch {
                                _ => 1
                    };
                }
            }
            """,
            ("skala_continuous_indent_multiplier", "3")
        );

    [Fact]
    public void WithoutContinuousIndentInsideBraces_TheContentsTakeOneWidth() =>
        Agrees(
            Source,
            """
            class C {
                void M() {
                    var list = new List<string> {
                        "an element long enough to keep the initializer broken across lines",
                        "a second element long enough to keep the initializer broken too"
                    };
                    var obj = new T {
                        Alpha = "an element long enough to keep the initializer broken across lines",
                        Bravo = "a second element long enough to keep the initializer broken too"
                    };
                    var s = a switch {
                        _ => 1
                    };
                }
            }
            """,
            ("skala_continuous_indent_multiplier", "2"),
            ("skala_use_continuous_indent_inside_initializer_braces", "false")
        );
}
