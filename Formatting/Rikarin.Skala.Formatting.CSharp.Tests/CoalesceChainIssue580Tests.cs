using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A <c>??</c> chain breaks as the right-associative tree it is (issue #580): before each <c>??</c>
///     whose right side does not fit, so <c>a</c> / <c>?? b ?? c</c>.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-08 with <c>Testing ask</c>.
/// </remarks>
public sealed class CoalesceChainIssue580Tests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    const string Owners = """
                          class T {
                              object M() {
                                  var someLongerName = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa;
                                  var someLongerName = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa ?? deltaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? epsilonnnnnnnnnnnnnnnnnnnnnnnnn ?? zetaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;
                                  var x = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;
                                  return alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa ?? deltaaaaaaaaaaaaaaaaaaaa;
                              }
                              void N() {
                                  Use(alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa ?? deltaaaaaaaaaaaaaaaaaaaa);
                                  var y = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                      ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa;
                              }
                          }
                          """;

    const string OwnersOracle = """
                                class T {
                                    object M() {
                                        var someLongerName = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                            ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa;
                                        var someLongerName = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                            ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                            ?? gammaaaaaaaaaaaaaaaaaa
                                            ?? deltaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                            ?? epsilonnnnnnnnnnnnnnnnnnnnnnnnn ?? zetaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;
                                        var x = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                            ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;
                                        return alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                            ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa ?? deltaaaaaaaaaaaaaaaaaaaa;
                                    }

                                    void N() {
                                        Use(
                                            alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                            ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa ?? deltaaaaaaaaaaaaaaaaaaaa
                                        );
                                        var y = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                            ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa;
                                    }
                                }
                                """;

    const string Neighbours = """
                              class T {
                                  object M(bool flag) {
                                      if ((alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaaaaaaaaaa) != null) { }
                                      var a = (alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa) ?? gammaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;
                                      var b = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? throw new System.InvalidOperationException("x");
                                      var c = alpha
                                          ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                          ?? gamma;
                                      var d = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? b ?? c ?? d;
                                      return alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Beta() ?? gammaaaaaaaaaaaaaaaaaaaa.Delta() ?? epsilon;
                                  }
                              }
                              """;

    const string NeighboursOracle = """
                                    class T {
                                        object M(bool flag) {
                                            if ((alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                    ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaaaaaaaaaa)
                                                != null) { }

                                            var a = (alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa)
                                                ?? gammaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;
                                            var b = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? throw new System.InvalidOperationException("x");
                                            var c = alpha
                                                ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                ?? gamma;
                                            var d = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                ?? b ?? c ?? d;
                                            return alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Beta()
                                                ?? gammaaaaaaaaaaaaaaaaaaaa.Delta() ?? epsilon;
                                        }
                                    }
                                    """;

    public static TheoryData<string, string> Cases =>
        new() { { Owners, OwnersOracle }, { Neighbours, NeighboursOracle } };

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheChain_ComesBackAsTheOracleWritesIt(string source, string expected) {
        var formatted = FormatWith(source);
        Assert.Equal(expected + "\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }
}
