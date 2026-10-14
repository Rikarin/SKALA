using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #603, group C: which constants a parameter's one attribute section chops by is decided by how many of its
///     arguments are positional — a constructor argument's <c>name:</c> counted, a property's <c>Name =</c> not. Two or
///     more take the two-argument rule, one or none the one-argument rule. Every expected string is jb cleanupcode
///     2025.2.6's own output, from the 2026-10-14 probes, one column short of the threshold and at it.
/// </summary>
public sealed class PositionalArgumentCountSectionChopIssue603Tests {
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
    ///     <c>Range(1, 99, ErrorMessage = "…")</c>, <c>Option('v', "…", Required = false)</c> and
    ///     <c>Bind(x: 1, y: "…")</c> chop by the two-argument constants; <c>Display("…", B = true, C = 2)</c> by the
    ///     one-argument constants.
    /// </summary>
    [Theory]
    [InlineData("rng_ic_i8_103")]
    [InlineData("rng_ic_i8_104")]
    [InlineData("opt_sn_i8_111")]
    [InlineData("opt_sn_i8_112")]
    [InlineData("c2_mc_i8_103")]
    [InlineData("c2_mc_i8_104")]
    [InlineData("p1n2_lb_i8_111")]
    [InlineData("p1n2_lb_i8_112")]
    public void ThePositionalArgumentsChooseTheConstants(string cell) {
        var (source, expected) = cell switch {
            "rng_ic_i8_103" => (
                """
                class C {
                    void M(
                        int b,
                        [Range(1, 99, ErrorMessage = "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijab")] int maximumCount = 0
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Range(1, 99, ErrorMessage = "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijab")]
                        int maximumCount = 0
                    ) { }
                }
                """
            ),
            "rng_ic_i8_104" => (
                """
                class C {
                    void M(
                        int b,
                        [Range(1, 99, ErrorMessage = "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc")] int maximumCount = 0
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Range(
                            1,
                            99,
                            ErrorMessage = "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc"
                        )]
                        int maximumCount = 0
                    ) { }
                }
                """
            ),
            "opt_sn_i8_111" => (
                """
                class C {
                    void M(
                        int b,
                        [Option('v', "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefgh", Required = false)] string displayName = null
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Option('v', "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefgh", Required = false)]
                        string displayName = null
                    ) { }
                }
                """
            ),
            "opt_sn_i8_112" => (
                """
                class C {
                    void M(
                        int b,
                        [Option('v', "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghi", Required = false)] string displayName = null
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Option(
                            'v',
                            "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghi",
                            Required = false
                        )]
                        string displayName = null
                    ) { }
                }
                """
            ),
            "c2_mc_i8_103" => (
                """
                class C {
                    void M(
                        int b,
                        [Bind(x: 1, y: "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdef")] int maximumCount
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Bind(x: 1, y: "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdef")] int maximumCount
                    ) { }
                }
                """
            ),
            "c2_mc_i8_104" => (
                """
                class C {
                    void M(
                        int b,
                        [Bind(x: 1, y: "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefg")] int maximumCount
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Bind(
                            x: 1,
                            y: "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefg"
                        )]
                        int maximumCount
                    ) { }
                }
                """
            ),
            "p1n2_lb_i8_111" => (
                """
                class C {
                    void M(
                        int b,
                        [Display("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc", B = true, C = 2)] string label
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Display("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc", B = true, C = 2)]
                        string label
                    ) { }
                }
                """
            ),
            "p1n2_lb_i8_112" => (
                """
                class C {
                    void M(
                        int b,
                        [Display("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcd", B = true, C = 2)] string label
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Display(
                            "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcd",
                            B = true,
                            C = 2
                        )]
                        string label
                    ) { }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
