using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #603: a parameter wider than eleven columns behind its one attribute section of one argument. Every expected
///     string is jb cleanupcode 2025.2.6's own output for the input, from the 2026-10-10 sweeps.
/// </summary>
public sealed class OneArgumentSectionChopIssue603Tests {
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

    /// <summary>
    ///     The argument chops from the threshold and one column short of it the section stays whole: the attribute's
    ///     name, the indent and the parameter move it.
    /// </summary>
    [Theory]
    [InlineData("dw12i8_112")]
    [InlineData("dw12i8_113")]
    [InlineData("oiw13i8_112")]
    [InlineData("oiw13i8_113")]
    [InlineData("niw15i8_116")]
    [InlineData("niw15i8_117")]
    [InlineData("blw13i8_110")]
    [InlineData("blw13i8_111")]
    public void ChopsFromTheMeasuredThreshold(string cell) {
        var (source, expected) = cell switch {
            "dw12i8_112" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Description("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Description("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        string aaaaa
                    ) { }
                }
                """
            ),
            "dw12i8_113" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Description("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Description(
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        string aaaaa
                    ) { }
                }
                """
            ),
            "oiw13i8_112" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        int aaaaaaaaa
                    ) { }
                }
                """
            ),
            "oiw13i8_113" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Obsolete(
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        int aaaaaaaaa
                    ) { }
                }
                """
            ),
            "niw15i8_116" => (
                """
                class C
                {
                    void M(
                        int b,
                        [DisplayName("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [DisplayName("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        int aaaaaaaaaaa
                    ) { }
                }
                """
            ),
            "niw15i8_117" => (
                """
                class C
                {
                    void M(
                        int b,
                        [DisplayName("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] int aaaaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [DisplayName(
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        int aaaaaaaaaaa
                    ) { }
                }
                """
            ),
            "blw13i8_110" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Ab("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] List<int> aaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Ab("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        List<int> aaa
                    ) { }
                }
                """
            ),
            "blw13i8_111" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Ab("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] List<int> aaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Ab(
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        List<int> aaa
                    ) { }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }

    /// <summary>
    ///     A parameter long enough for its indent and attribute name keeps the section whole at every width.
    /// </summary>
    [Theory]
    [InlineData("aw16i8_120")]
    public void NeverChopsPastTheSecondCondition(string cell) {
        var (source, expected) = cell switch {
            "aw16i8_120" => (
                """
                class C
                {
                    void M(
                        int b,
                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
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
