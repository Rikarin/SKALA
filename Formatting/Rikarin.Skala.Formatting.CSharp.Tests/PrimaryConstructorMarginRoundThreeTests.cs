using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     SK-DIV-0198: the base name&apos;s and the interfaces&apos; margins. Every expected string is <c>jb cleanupcode</c>
///     2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class PrimaryConstructorMarginRoundThreeTests {
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
    ///     SK-DIV-0198 round three: the interfaces&apos; continuation margin is 12 behind a twelve-letter base and 18
    ///     behind a one-letter one; the lone base type&apos;s 31 and 33.
    /// </summary>
    [Fact]
    public void TheMarginsFollowTheBaseNameAndTheInterfaces() {
        Agrees(
            """
            class O1 {
                class O2 {
                    class IA108(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class IA108(int alphaValue, int betaValue)
                        : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb),
                            IFirstInterface,
                            ISecondInterface { }
                }
            }
            """
        );
        Agrees(
            """
            class O1 {
                class O2 {
                    class IA109(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class IA109(int alphaValue, int betaValue) : BaseTypeName(
                            alphaValueArgument,
                            betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                        ),
                        IFirstInterface,
                        ISecondInterface { }
                }
            }
            """
        );
        Agrees(
            """
            class O1 {
                class O2 {
                    class ID108(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, gammaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class ID108(int alphaValue, int betaValue)
                        : BaseTypeName(alphaValueArgument, gammaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb),
                            IFirstInterface,
                            ISecondInterface { }
                }
            }
            """
        );
        Agrees(
            """
            class O1 {
                class O2 {
                    class ID109(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, gammaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class ID109(int alphaValue, int betaValue) : BaseTypeName(
                            alphaValueArgument,
                            gammaValueArgument,
                            betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                        ),
                        IFirstInterface,
                        ISecondInterface { }
                }
            }
            """
        );
        Agrees(
            """
            class O1 {
                class O2 {
                    class IE102(int alphaValue, int betaValue) : B(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class IE102(int alphaValue, int betaValue)
                        : B(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb),
                            IFirstInterface,
                            ISecondInterface { }
                }
            }
            """
        );
        Agrees(
            """
            class O1 {
                class O2 {
                    class IE103(int alphaValue, int betaValue) : B(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class IE103(int alphaValue, int betaValue) : B(
                            alphaValueArgument,
                            betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                        ),
                        IFirstInterface,
                        ISecondInterface { }
                }
            }
            """
        );
        Agrees(
            """
            class O1 {
                class O2 {
                    class SE86(int alphaValue, int betaValue) : B(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class SE86(int alphaValue, int betaValue)
                        : B(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }
                }
            }
            """
        );
        Agrees(
            """
            class O1 {
                class O2 {
                    class SE87(int alphaValue, int betaValue) : B(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class SE87(int alphaValue, int betaValue) : B(
                        alphaValueArgument,
                        betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                    ) { }
                }
            }
            """
        );
    }
}
