using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
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
    const string Long5 = "var someLongerName = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaa"
        + "aaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa;";

    const string Long6 = "?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa ?? deltaaaaa"
        + "aaaaaaaaaaaaaaa;";

    const string Long7 = "if ((alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        + " ?? gammaaaaaaaaaaaaaaaaaaaaaaaaaa) != null) { }";

    const string Long8 = "var a = (alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        + "aaaa) ?? gammaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;";

    const string Long9 = "return alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.Beta() ??"
        + " gammaaaaaaaaaaaaaaaaaaaa.Delta() ?? epsilon;";

    const string Long10 = "?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? throw new System.InvalidOperationEx"
        + "ception(\"x\");";

    const string Long1 = "var someLongerName = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaa"
        + "aaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa ?? deltaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        + "aaaaaaaa ?? epsilonnnnnnnnnnnnnnnnnnnnnnnnn ?? zetaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        + "aaaaa;";

    const string Long2 = "return alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        + "aa ?? gammaaaaaaaaaaaaaaaaaa ?? deltaaaaaaaaaaaaaaaaaaaa;";

    const string Long3 = "Use(alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa "
        + "?? gammaaaaaaaaaaaaaaaaaa ?? deltaaaaaaaaaaaaaaaaaaaa);";

    const string Long4 = "var b = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        + "aaa ?? throw new System.InvalidOperationException(\"x\");";

    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    static readonly string Owners = $$"""
                                      class T {
                                          object M() {
                                              {{Long5}}
                                              {{Long1}}
                                              var x = alph{{R('a', 68)}} ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;
                                              {{Long2}}
                                          }
                                          void N() {
                                              {{Long3}}
                                              var y = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                  ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? gammaaaaaaaaaaaaaaaaaa;
                                          }
                                      }
                                      """;

    static readonly string OwnersOracle = $$"""
                                            class T {
                                                object M() {
                                                    var someLongerName = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                        ?? bet{{R('a', 35)}} ?? gammaaaaaaaaaaaaaaaaaa;
                                                    var someLongerName = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                        ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                        ?? gammaaaaaaaaaaaaaaaaaa
                                                        ?? deltaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                        ?? epsilonnnnnnnnnnnnnnnnnnnnnnnnn ?? zet{{R('a', 35)}};
                                                    var x = alph{{R('a', 68)}}
                                                        ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;
                                                    return alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                        {{Long6}}
                                                }

                                                void N() {
                                                    Use(
                                                        alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                        ?? bet{{R('a', 35)}} ?? gamm{{R('a', 18)}} ?? delt{{R('a', 20)}}
                                                    );
                                                    var y = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                        ?? bet{{R('a', 35)}} ?? gammaaaaaaaaaaaaaaaaaa;
                                                }
                                            }
                                            """;

    static readonly string Neighbours = $$"""
                                          class T {
                                              object M(bool flag) {
                                                  {{Long7}}
                                                  {{Long8}}
                                                  {{Long4}}
                                                  var c = alpha
                                                      ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                      ?? gamma;
                                                  var d = alph{{R('a', 88)}} ?? b ?? c ?? d;
                                                  {{Long9}}
                                              }
                                          }
                                          """;

    static readonly string NeighboursOracle = $$"""
                                                class T {
                                                    object M(bool flag) {
                                                        if ((alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                                ?? bet{{R('a', 35)}} ?? gammaaaaaaaaaaaaaaaaaaaaaaaaaa)
                                                            != null) { }

                                                        var a = (alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ?? bet{{R('a', 35)}})
                                                            ?? gammaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa;
                                                        var b = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                            {{Long10}}
                                                        var c = alpha
                                                            ?? betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                                                            ?? gamma;
                                                        var d = alph{{R('a', 88)}}
                                                            ?? b ?? c ?? d;
                                                        return alph{{R('a', 59)}}.Beta()
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
