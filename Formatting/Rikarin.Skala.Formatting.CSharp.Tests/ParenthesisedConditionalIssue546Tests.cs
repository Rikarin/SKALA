using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A conditional directly inside a grouping parenthesis (issue #546): its signs land one level past
///     the parenthesis's line, not two.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>
///     under the repository's configuration. A conditional that is an argument is the control and
///     keeps its own level.
/// </remarks>
public sealed class ParenthesisedConditionalIssue546Tests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Owners = """
                          class T {
                              int M(bool b, int a, int c) {
                                  int w1 = b ? (a > 0 ? a
                                  : c) : c;
                                  int w2 = (a > 0 ? a
                                  : c);
                                  F((a > 0 ? a
                                  : c));
                                  if ((a > 0 ? a
                                  : c) > 1) { }
                                  int w3 = b ? (a
                                  + 1) : c;
                                  int w4 = c + (a > 0 ? a
                                  : c);
                                  int w5 = b ? F(a > 0 ? a
                                  : c) : c;
                                  int w6 = b ? a : (a > 0 ? a
                                  : c);
                                  return w1 + w2 + w3 + w4 + w5 + w6;
                              }
                              int F(int a, int b = 0) => a;
                          }
                          """;

    const string OwnersOracle = """
                                class T {
                                    int M(bool b, int a, int c) {
                                        int w1 = b
                                            ? (a > 0
                                                ? a
                                                : c)
                                            : c;
                                        int w2 = (a > 0
                                            ? a
                                            : c);
                                        F(
                                            (a > 0
                                                ? a
                                                : c)
                                        );
                                        if ((a > 0
                                                ? a
                                                : c)
                                            > 1) { }

                                        int w3 = b
                                            ? (a
                                                + 1)
                                            : c;
                                        int w4 = c
                                            + (a > 0
                                                ? a
                                                : c);
                                        int w5 = b
                                            ? F(
                                                a > 0
                                                    ? a
                                                    : c
                                            )
                                            : c;
                                        int w6 = b
                                            ? a
                                            : (a > 0
                                                ? a
                                                : c);
                                        return w1 + w2 + w3 + w4 + w5 + w6;
                                    }

                                    int F(int a, int b = 0) => a;
                                }
                                """;

    const string Neighbours = """
                              class T {
                                  string M(bool b, int a, int c) {
                                      var s = (a > 0
                                      ? a
                                      : c).ToString();
                                      var l = (long)(a > 0
                                      ? a
                                      : c);
                                      var n = (a > 0 ? a
                                      : b ? c
                                      : 0);
                                      return (a > 0
                                      ? "x"
                                      : "y");
                                  }
                              }
                              """;

    const string NeighboursOracle = """
                                    class T {
                                        string M(bool b, int a, int c) {
                                            var s = (a > 0
                                                ? a
                                                : c).ToString();
                                            var l = (long)(a > 0
                                                ? a
                                                : c);
                                            var n = (a > 0 ? a
                                                : b ? c
                                                : 0);
                                            return (a > 0
                                                ? "x"
                                                : "y");
                                        }
                                    }
                                    """;

    public static TheoryData<string, string> Cases =>
        new() {
            { Owners, OwnersOracle },
            { Neighbours, NeighboursOracle }
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheConditional_ComesBackAsTheOracleWritesIt(string source, string expected) {
        var formatted = FormatWith(source);
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
