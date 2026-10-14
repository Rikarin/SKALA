using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #603: behind a long parameter's default value the oracle breaks the default's = while the line through it
///     fits
///     and the arguments do not chop, then puts the parameter below the section. Every expected string is jb cleanupcode
///     2025.2.6's own output for the input, from the 2026-10-10 sweeps.
/// </summary>
public sealed class DefaultValueEqualsBandIssue603Tests {
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
    ///     The = breaks while the line through it fits, the parameter goes below the section from the next column, and
    ///     the arguments chop by the measured rule.
    /// </summary>
    [Theory]
    [InlineData("d2dw16i8_111")]
    [InlineData("d2dw16i8_112")]
    [InlineData("d2dw16i8_117")]
    [InlineData("jrnw16i8_108")]
    [InlineData("jrnw16i8_109")]
    public void TheEqualsBandIsAsWideAsTheValue(string cell) {
        var (source, expected) = cell switch {
            "d2dw16i8_111" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Order = 1)] int aa = default
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Order = 1)] int aa =
                            default
                    ) { }
                }
                """
            ),
            "d2dw16i8_112" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Order = 1)] int aa = default
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Order = 1)]
                        int aa = default
                    ) { }
                }
                """
            ),
            "d2dw16i8_117" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Order = 1)] int aa = default
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Display(
                            Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            Order = 1
                        )]
                        int aa = default
                    ) { }
                }
                """
            ),
            "jrnw16i8_108" => (
                """
                class C
                {
                    void M(
                        int b,
                        [JsonProperty("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Required = Required.Always)] string aa = null
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [JsonProperty("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Required = Required.Always)] string aa =
                            null
                    ) { }
                }
                """
            ),
            "jrnw16i8_109" => (
                """
                class C
                {
                    void M(
                        int b,
                        [JsonProperty("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Required = Required.Always)] string aa = null
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [JsonProperty("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Required = Required.Always)]
                        string aa = null
                    ) { }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
