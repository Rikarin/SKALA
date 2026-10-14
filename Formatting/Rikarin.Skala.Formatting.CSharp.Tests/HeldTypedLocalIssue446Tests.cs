using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #446 item 2, SK-DIV-0211 / SK-DIV-0005: a typed local before a single call on a receiver, where the value
///     does not fit beside the <c>=</c>. Each pair is the widest line below that breaks the <c>=</c> and the next column,
///     which chops. Every expected string is jb cleanupcode 2025.2.6's own output for the input, from the 2026-10-14
///     sweeps (<see cref="EqualsFloor.HeldTypedLocalBreaks" />).
/// </summary>
public sealed class HeldTypedLocalIssue446Tests {
    static void Agrees(string source, string expected) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
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
    ///     A type of 24 lowers the limit to 110 (round 3's rule allowed 112); a type of 63 raises it to 113 (round 3's
    ///     rule stopped at 112); a name of 30 lowers it to 108 (round 3: 112).
    /// </summary>
    [Theory]
    [InlineData("ty24_110")]
    [InlineData("ty24_111")]
    [InlineData("ty63_113")]
    [InlineData("ty63_114")]
    [InlineData("nm30_108")]
    [InlineData("nm30_109")]
    public void TheLimitFollowsTheTypeAndTheName(string cell) {
        var (source, expected) = Cells[cell];
        Agrees(source, expected);
    }

    static readonly Dictionary<string, (string Source, string Expected)> Cells = new() {
            ["ty24_110"] = (
                """
                class C
                {
                    object M()
                    {
                        Tyyyyyyyyyyyyyyyyyyyyyyy daaaa = xxxx.Mmmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
                    }
                }
                """,
                """
                class C {
                    object M() {
                        Tyyyyyyyyyyyyyyyyyyyyyyy daaaa =
                            xxxx.Mmmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
                    }
                }
                """
            ),
            ["ty24_111"] = (
                """
                class C
                {
                    object M()
                    {
                        Tyyyyyyyyyyyyyyyyyyyyyyy daaaa = xxxx.Mmmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
                    }
                }
                """,
                """
                class C {
                    object M() {
                        Tyyyyyyyyyyyyyyyyyyyyyyy daaaa = xxxx.Mmmmmmmmmmmmmm(
                            vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
                        );
                    }
                }
                """
            ),
            ["ty63_113"] = (
                """
                class C
                {
                    object M()
                    {
                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaa = xxxx.Mmmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
                    }
                }
                """,
                """
                class C {
                    object M() {
                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaa =
                            xxxx.Mmmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
                    }
                }
                """
            ),
            ["ty63_114"] = (
                """
                class C
                {
                    object M()
                    {
                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaa = xxxx.Mmmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
                    }
                }
                """,
                """
                class C {
                    object M() {
                        Tyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy daaaa = xxxx.Mmmmmmmmmmmmmm(
                            vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
                        );
                    }
                }
                """
            ),
            ["nm30_108"] = (
                """
                class C
                {
                    object M()
                    {
                        Tyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaa = xxxx.Mmmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
                    }
                }
                """,
                """
                class C {
                    object M() {
                        Tyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaa =
                            xxxx.Mmmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
                    }
                }
                """
            ),
            ["nm30_109"] = (
                """
                class C
                {
                    object M()
                    {
                        Tyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaa = xxxx.Mmmmmmmmmmmmmm(vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv);
                    }
                }
                """,
                """
                class C {
                    object M() {
                        Tyyyyyyyyy daaaaaaaaaaaaaaaaaaaaaaaaaaaaa = xxxx.Mmmmmmmmmmmmmm(
                            vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
                        );
                    }
                }
                """
            ),
    };
}
