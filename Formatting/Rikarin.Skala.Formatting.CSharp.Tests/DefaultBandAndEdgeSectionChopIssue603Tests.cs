using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #603, round nine (SK-DIV-0570): inside a default's band a two-argument section chops about four columns
///     later than its own rule and a one-argument section never does, and a one-argument section's second condition
///     depends on the section's end. Every expected string is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class DefaultBandAndEdgeSectionChopIssue603Tests {
    /// <summary>The oracle's answer under the repository's export, and a second pass that changes nothing.</summary>
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

    /// <summary>Inside the band where the line through the default's = fits, the = breaks a few columns past the section's own chop, and a one-argument section's never chops there.</summary>
    [Theory]
    [InlineData("c603b:c2_bf_i12_104")]
    [InlineData("c603b:c2_bf_i12_105")]
    [InlineData("c603b:c2_bf_i12_106")]
    [InlineData("p603n:ppdw19i8_104")]
    [InlineData("p603n:ppdw19i8_106")]
    [InlineData("p603n:ppdw19i8_107")]
    [InlineData("j1:av7w20_i8_106")]
    [InlineData("j1:av7w20_i8_107")]
    [InlineData("p603n:fbtw13i8_111")]
    public void ADefaultsBandKeepsTheSectionWholeLonger(string cell) {
        var (source, expected) = cell switch {
            "c603b:c2_bf_i12_104" => (
                """
                class C {
                    class D0 {
                        void M(
                            int b,
                            [Bind(x: 1, y: "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc")] bool flag = false
                        ) { }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        void M(
                            int b,
                            [Bind(x: 1, y: "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc")] bool flag =
                                false
                        ) { }
                    }
                }
                """
            ),
            "c603b:c2_bf_i12_105" => (
                """
                class C {
                    class D0 {
                        void M(
                            int b,
                            [Bind(x: 1, y: "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcd")] bool flag = false
                        ) { }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        void M(
                            int b,
                            [Bind(x: 1, y: "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcd")] bool flag =
                                false
                        ) { }
                    }
                }
                """
            ),
            "c603b:c2_bf_i12_106" => (
                """
                class C {
                    class D0 {
                        void M(
                            int b,
                            [Bind(x: 1, y: "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcde")] bool flag = false
                        ) { }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        void M(
                            int b,
                            [Bind(
                                x: 1,
                                y: "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcde"
                            )]
                            bool flag = false
                        ) { }
                    }
                }
                """
            ),
            "p603n:ppdw19i8_104" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaa = default
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaa =
                            default
                    ) { }
                }
                """
            ),
            "p603n:ppdw19i8_106" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaa = default
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaa =
                            default
                    ) { }
                }
                """
            ),
            "p603n:ppdw19i8_107" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaa = default
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Range(
                            1,
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        int aaaaa = default
                    ) { }
                }
                """
            ),
            "j1:av7w20_i8_106" => (
                """
                class C {
                    void M(
                        int b,
                        [A(1, "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefgh")] T pqqqqqqq = default
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A(1, "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefgh")] T pqqqqqqq =
                            default
                    ) { }
                }
                """
            ),
            "j1:av7w20_i8_107" => (
                """
                class C {
                    void M(
                        int b,
                        [A(1, "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghi")] T pqqqqqqq = default
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A(
                            1,
                            "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghi"
                        )]
                        T pqqqqqqq = default
                    ) { }
                }
                """
            ),
            "p603n:fbtw13i8_111" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Foo("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Bar = true)] bool a = true
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Foo("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Bar = true)] bool a =
                            true
                    ) { }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }

    /// <summary>Near the margin a one-argument section stays whole past its first condition, until a second, steeper one.</summary>
    [Theory]
    [InlineData("p476e:As15i8_113")]
    [InlineData("p476e:As15i8_117")]
    [InlineData("p476e:As15i8_118")]
    [InlineData("p603f:biw15i8_115")]
    [InlineData("p603f:biw15i8_116")]
    [InlineData("p603:fw16i8_116")]
    [InlineData("p603:fw16i8_118")]
    public void AOneArgumentSectionBendsUpTowardTheMargin(string cell) {
        var (source, expected) = cell switch {
            "p476e:As15i8_113" => (
                """
                class C
                {
                    void M(
                        int b,
                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string? aaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        string? aaaaaaa
                    ) { }
                }
                """
            ),
            "p476e:As15i8_117" => (
                """
                class C
                {
                    void M(
                        int b,
                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string? aaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        string? aaaaaaa
                    ) { }
                }
                """
            ),
            "p476e:As15i8_118" => (
                """
                class C
                {
                    void M(
                        int b,
                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string? aaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A(
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        string? aaaaaaa
                    ) { }
                }
                """
            ),
            "p603f:biw15i8_115" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Ab("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Ab("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        int aaaaaaaaaaa
                    ) { }
                }
                """
            ),
            "p603f:biw15i8_116" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Ab("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Ab(
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        int aaaaaaaaaaa
                    ) { }
                }
                """
            ),
            "p603:fw16i8_116" => (
                """
                class C
                {
                    void M(
                        int b,
                        [From("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [From("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        string aaaaaaaaa
                    ) { }
                }
                """
            ),
            "p603:fw16i8_118" => (
                """
                class C
                {
                    void M(
                        int b,
                        [From("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [From("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        string aaaaaaaaa
                    ) { }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
