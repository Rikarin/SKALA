using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     SK-DIV-0198: the margin of a primary constructor&apos;s lone base type. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class PrimaryConstructorMarginTests {
    const string Long3 = "class SA87(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, bet"
        + "abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }";

    const string Long4 = "class SA88(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, bet"
        + "abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }";

    const string Long5 = "class SA89(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, bet"
        + "abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }";

    const string Long6 = "class SA95(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, bet"
        + "abbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }";

    const string Long1 = "class SD88(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, gam"
        + "maValueArgument, betabbbbbbbbbbbb) { }";

    const string Long2 = "class SD89(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, gam"
        + "maValueArgument, betabbbbbbbbbbbbb) { }";

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
    ///     SK-DIV-0198&apos;s margin: a primary constructor&apos;s lone base type breaks before the colon up to an
    ///     88-column continuation line nested, 89 at the top level, and keeps : B( past it.
    /// </summary>
    [Fact]
    public void ALoneBaseType_BreaksBeforeTheColonOnlyToAnEightyEightColumnContinuation() {
        Agrees(
            $$"""
              class O1 {
                  class O2 {
                      {{Long3}}
                  }
              }
              """,
            """
            class O1 {
                class O2 {
                    class SA87(int alphaValue, int betaValue)
                        : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }
                }
            }
            """
        );
        Agrees(
            $$"""
              class O1 {
                  class O2 {
                      {{Long4}}
                  }
              }
              """,
            """
            class O1 {
                class O2 {
                    class SA88(int alphaValue, int betaValue)
                        : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }
                }
            }
            """
        );
        Agrees(
            $$"""
              class O1 {
                  class O2 {
                      {{Long5}}
                  }
              }
              """,
            """
            class O1 {
                class O2 {
                    class SA89(int alphaValue, int betaValue) : BaseTypeName(
                        alphaValueArgument,
                        betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                    ) { }
                }
            }
            """
        );
        Agrees(
            $$"""
              class O1 {
                  class O2 {
                      {{Long6}}
                  }
              }
              """,
            """
            class O1 {
                class O2 {
                    class SA95(int alphaValue, int betaValue) : BaseTypeName(
                        alphaValueArgument,
                        betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                    ) { }
                }
            }
            """
        );
        Agrees(
            $$"""
              class O1 {
                  class O2 {
                      {{Long1}}
                  }
              }
              """,
            """
            class O1 {
                class O2 {
                    class SD88(int alphaValue, int betaValue)
                        : BaseTypeName(alphaValueArgument, gammaValueArgument, betabbbbbbbbbbbb) { }
                }
            }
            """
        );
        Agrees(
            $$"""
              class O1 {
                  class O2 {
                      {{Long2}}
                  }
              }
              """,
            """
            class O1 {
                class O2 {
                    class SD89(int alphaValue, int betaValue) : BaseTypeName(
                        alphaValueArgument,
                        gammaValueArgument,
                        betabbbbbbbbbbbbb
                    ) { }
                }
            }
            """
        );
        Agrees(
            $$"""
              class SB88(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, beta{{R('b', 40)}}) { }
              """,
            """
            class SB88(int alphaValue, int betaValue)
                : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }
            """
        );
        Agrees(
            $$"""
              class SB90(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, beta{{R('b', 42)}}) { }
              """,
            """
            class SB90(int alphaValue, int betaValue) : BaseTypeName(
                alphaValueArgument,
                betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            ) { }
            """
        );
        Agrees(
            $$"""
              class SB91(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, beta{{R('b', 43)}}) { }
              """,
            """
            class SB91(int alphaValue, int betaValue) : BaseTypeName(
                alphaValueArgument,
                betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            ) { }
            """
        );
    }
}
