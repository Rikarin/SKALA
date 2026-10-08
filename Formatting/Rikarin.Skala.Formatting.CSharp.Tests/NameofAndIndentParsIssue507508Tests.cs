using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issues #507 and #508 (SK-DIV-0204): <c>nameof(…)</c> is laid out as the <c>typeof</c> family rather than as
///     a call, and <c>skala_indent_pars</c> governs that family and brackets but not a grouping parenthesis, nor —
///     at <c>none</c> — a positional pattern or a tuple. Every expected string is <c>jb cleanupcode</c>'s own
///     output for the input under the export with the override shown, and each test asserts the second pass too.
/// </summary>
public sealed class NameofAndIndentParsIssue507508Tests {
    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
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

    const string Shapes = """
                          class C {
                              void M() {
                                  var n1 = nameof(a
                                  );
                                  var n3 = nameof(
                                      a
                                  );
                                  var t1 = typeof(int
                                  );
                                  Call(nameof(a
                                  ), b);
                                  Call(1, (a + b
                                  ));
                                  if ((a
                                      ) == b) { }
                                  var t = typeof(
                                      int);
                                  var k = checked(a
                                      + b);
                                  var g = (a
                                      + b);
                                  var u = (1,
                                      2);
                                  var e = arr[
                                      1];
                                  bool P(object o) => o is (1,
                                      2);
                              }
                          }
                          """;

    /// <summary>
    ///     #507: <c>nameof</c> keeps every break the author wrote and puts a kept <c>)</c> on its opener's line;
    ///     a call around it chops because it holds a line break.
    /// </summary>
    [Fact]
    public void Nameof_KeepsItsBreaksAsTypeofDoes() =>
        Agrees(
            """
            class C {
                void M() {
                    var n1 = nameof(a
                    );
                    var n2 = nameof(
                        a);
                    Call(nameof(a
                    ), b);
                }
            }
            """,
            """
            class C {
                void M() {
                    var n1 = nameof(a
                    );
                    var n2 = nameof(
                        a);
                    Call(
                        nameof(a
                        ),
                        b
                    );
                }
            }
            """
        );

    /// <summary>
    ///     #508 at <c>outside</c>: a <c>typeof</c>'s and a <c>nameof</c>'s kept <c>)</c> take the extra level, a
    ///     grouping parenthesis's does not — inside an argument list and in a condition alike.
    /// </summary>
    [Fact]
    public void Outside_GovernsTheTypeofFamilyAndNotAGrouping() =>
        Agrees(
            Shapes,
            """
            class C {
                void M() {
                    var n1 = nameof(a
                        );
                    var n3 = nameof(
                        a
                        );
                    var t1 = typeof(int
                        );
                    Call(
                        nameof(a
                            ),
                        b
                    );
                    Call(
                        1,
                        (a + b
                        )
                    );
                    if ((a
                        )
                        == b) { }

                    var t = typeof(
                        int);
                    var k = checked(a
                        + b);
                    var g = (a
                        + b);
                    var u = (1,
                        2);
                    var e = arr[
                        1];

                    bool P(object o) =>
                        o is (1,
                            2);
                }
            }
            """,
            ("skala_indent_pars", "outside")
        );

    /// <summary>
    ///     #508 at <c>none</c>: the <c>typeof</c> family's and a bracket's contents go to the statement's column; a
    ///     grouping, a tuple and a positional pattern keep their one level.
    /// </summary>
    [Fact]
    public void None_GovernsTheTypeofFamilyAndBrackets() =>
        Agrees(
            Shapes,
            """
            class C {
                void M() {
                    var n1 = nameof(a
                    );
                    var n3 = nameof(
                    a
                    );
                    var t1 = typeof(int
                    );
                    Call(
                        nameof(a
                        ),
                        b
                    );
                    Call(
                        1,
                        (a + b
                        )
                    );
                    if ((a
                        )
                        == b) { }

                    var t = typeof(
                    int);
                    var k = checked(a
                    + b);
                    var g = (a
                        + b);
                    var u = (1,
                        2);
                    var e = arr[
                    1];

                    bool P(object o) =>
                        o is (1,
                            2);
                }
            }
            """,
            ("skala_indent_pars", "none")
        );
}
