using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #603: a parameter wider than eleven columns behind a section with a named argument, and a parameter with a
///     default value. Every expected string is jb cleanupcode 2025.2.6's own output for the input, from the 2026-10-10
///     sweeps.
/// </summary>
public sealed class NamedArgumentAndDefaultSectionChopIssue603Tests {
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
    ///     A section with a named argument chops where a one-argument section would, at the threshold and not a column
    ///     short of it.
    /// </summary>
    [Theory]
    [InlineData("dnsw13i8_112")]
    [InlineData("dnsw13i8_113")]
    [InlineData("d2nw16i8_116")]
    [InlineData("d2nw16i8_117")]
    [InlineData("jrsw13i16_118")]
    [InlineData("jrsw13i16_119")]
    public void ANamedArgumentChopsByTheOneArgumentConstants(string cell) {
        var (source, expected) = cell switch {
            "dnsw13i8_112" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        string aaaaaa
                    ) { }
                }
                """
            ),
            "dnsw13i8_113" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Display(
                            Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        string aaaaaa
                    ) { }
                }
                """
            ),
            "d2nw16i8_116" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Order = 1)] string aa = null
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Order = 1)]
                        string aa = null
                    ) { }
                }
                """
            ),
            "d2nw16i8_117" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Display(Name = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Order = 1)] string aa = null
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
                        string aa = null
                    ) { }
                }
                """
            ),
            "jrsw13i16_118" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            void M(
                                int b,
                                [JsonProperty("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Required = Required.Always)] string aaaaaa
                            ) { }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            void M(
                                int b,
                                [JsonProperty("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Required = Required.Always)]
                                string aaaaaa
                            ) { }
                        }
                    }
                }
                """
            ),
            "jrsw13i16_119" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            void M(
                                int b,
                                [JsonProperty("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Required = Required.Always)] string aaaaaa
                            ) { }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            void M(
                                int b,
                                [JsonProperty(
                                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                                    Required = Required.Always
                                )]
                                string aaaaaa
                            ) { }
                        }
                    }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }

    /// <summary>
    ///     A parameter with a default value is measured whole, its value included.
    /// </summary>
    [Theory]
    [InlineData("ppnw22i8_106")]
    [InlineData("ppnw22i8_107")]
    [InlineData("pptw25i16_114")]
    [InlineData("pptw25i16_115")]
    public void ADefaultValueIsMeasuredWithTheParameter(string cell) {
        var (source, expected) = cell switch {
            "ppnw22i8_106" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaaaaa = null
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        string aaaaaaaa = null
                    ) { }
                }
                """
            ),
            "ppnw22i8_107" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaaaaa = null
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
                        string aaaaaaaa = null
                    ) { }
                }
                """
            ),
            "pptw25i16_114" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            void M(
                                int b,
                                [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] bool aaaaaaaaaaaaa = true
                            ) { }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            void M(
                                int b,
                                [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                                bool aaaaaaaaaaaaa = true
                            ) { }
                        }
                    }
                }
                """
            ),
            "pptw25i16_115" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            void M(
                                int b,
                                [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] bool aaaaaaaaaaaaa = true
                            ) { }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            void M(
                                int b,
                                [Range(
                                    1,
                                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                                )]
                                bool aaaaaaaaaaaaa = true
                            ) { }
                        }
                    }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
