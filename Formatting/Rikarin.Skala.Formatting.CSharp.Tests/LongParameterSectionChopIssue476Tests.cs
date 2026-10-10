using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #476, SK-DIV-0352: a parameter wider than eleven columns behind its one attribute section of two or more
///     positional arguments. Every expected string is jb cleanupcode 2025.2.6's own output for the input, from the
///     2026-10-09 sweeps.
/// </summary>
public sealed class LongParameterSectionChopIssue476Tests {
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
    ///     The arguments chop from the measured threshold and one column short of it stay whole, the parameter alone below:
    ///     the attribute's name, the indent and the parameter move it; what the arguments are does not.
    /// </summary>
    [Theory]
    [InlineData("i19i8_104")]
    [InlineData("i19i8_105")]
    [InlineData("l25i12_113")]
    [InlineData("l25i12_114")]
    [InlineData("d25i16_115")]
    [InlineData("d25i16_116")]
    [InlineData("Rs19i8_102")]
    [InlineData("Rs19i8_103")]
    [InlineData("Ts21i20_109")]
    [InlineData("Ts21i20_111")]
    [InlineData("j17w20i12_111")]
    [InlineData("j17w20i12_112")]
    [InlineData("v13w23i24_118")]
    [InlineData("v13w23i24_119")]
    [InlineData("d8w20i24_112")]
    [InlineData("d8w20i24_113")]
    public void ChopsFromTheMeasuredThreshold(string cell) {
        var (source, expected) = cell switch {
            "i19i8_104" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] int aaaaaaaaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
                        int aaaaaaaaaaaaaaa
                    ) { }
                }
                """
            ),
            "i19i8_105" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] int aaaaaaaaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Obsolete(
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                            true
                        )]
                        int aaaaaaaaaaaaaaa
                    ) { }
                }
                """
            ),
            "l25i12_113" => (
                """
                class C
                {
                    class D8
                    {
                        void M(
                            int b,
                            [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] List<string> aaaaaaaaaaaa
                        ) { }
                    }
                }
                """,
                """
                class C {
                    class D8 {
                        void M(
                            int b,
                            [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
                            List<string> aaaaaaaaaaaa
                        ) { }
                    }
                }
                """
            ),
            "l25i12_114" => (
                """
                class C
                {
                    class D8
                    {
                        void M(
                            int b,
                            [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] List<string> aaaaaaaaaaaa
                        ) { }
                    }
                }
                """,
                """
                class C {
                    class D8 {
                        void M(
                            int b,
                            [Obsolete(
                                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                                true
                            )]
                            List<string> aaaaaaaaaaaa
                        ) { }
                    }
                }
                """
            ),
            "d25i16_115" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            void M(
                                int b,
                                [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] Dictionary<string, int> a
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
                                [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
                                Dictionary<string, int> a
                            ) { }
                        }
                    }
                }
                """
            ),
            "d25i16_116" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            void M(
                                int b,
                                [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] Dictionary<string, int> a
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
                                [Obsolete(
                                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                                    true
                                )]
                                Dictionary<string, int> a
                            ) { }
                        }
                    }
                }
                """
            ),
            "Rs19i8_102" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string? aaaaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        string? aaaaaaaaaaa
                    ) { }
                }
                """
            ),
            "Rs19i8_103" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Range(1, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string? aaaaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Range(
                            1,
                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        )]
                        string? aaaaaaaaaaa
                    ) { }
                }
                """
            ),
            "Ts21i20_109" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            class D16
                            {
                                void M(
                                    int b,
                                    [Foo(typeof(int), "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string? aaaaaaaaaaaaa
                                ) { }
                }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            class D16 {
                                void M(
                                    int b,
                                    [Foo(typeof(int), "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                                    string? aaaaaaaaaaaaa
                                ) { }
                            }
                        }
                    }
                }
                """
            ),
            "Ts21i20_111" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            class D16
                            {
                                void M(
                                    int b,
                                    [Foo(typeof(int), "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string? aaaaaaaaaaaaa
                                ) { }
                }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            class D16 {
                                void M(
                                    int b,
                                    [Foo(
                                        typeof(int),
                                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                                    )]
                                    string? aaaaaaaaaaaaa
                                ) { }
                            }
                        }
                    }
                }
                """
            ),
            "j17w20i12_111" => (
                """
                class C
                {
                    class D8
                    {
                        void M(
                            int b,
                            [JsonPropertyName("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 3)] string aaaaaaaaaaaaa
                        ) { }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        void M(
                            int b,
                            [JsonPropertyName("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 3)]
                            string aaaaaaaaaaaaa
                        ) { }
                    }
                }
                """
            ),
            "j17w20i12_112" => (
                """
                class C
                {
                    class D8
                    {
                        void M(
                            int b,
                            [JsonPropertyName("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 3)] string aaaaaaaaaaaaa
                        ) { }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        void M(
                            int b,
                            [JsonPropertyName(
                                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                                3
                            )]
                            string aaaaaaaaaaaaa
                        ) { }
                    }
                }
                """
            ),
            "v13w23i24_118" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            class D16
                            {
                                class D20
                                {
                                    void M(
                                        int b,
                                        [DefaultValue("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", typeof(int))] string aaaaaaaaaaaaaaaa
                                    ) { }
                }
                }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            class D16 {
                                class D20 {
                                    void M(
                                        int b,
                                        [DefaultValue("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", typeof(int))]
                                        string aaaaaaaaaaaaaaaa
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "v13w23i24_119" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            class D16
                            {
                                class D20
                                {
                                    void M(
                                        int b,
                                        [DefaultValue("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", typeof(int))] string aaaaaaaaaaaaaaaa
                                    ) { }
                }
                }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            class D16 {
                                class D20 {
                                    void M(
                                        int b,
                                        [DefaultValue(
                                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                                            typeof(int)
                                        )]
                                        string aaaaaaaaaaaaaaaa
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "d8w20i24_112" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            class D16
                            {
                                class D20
                                {
                                    void M(
                                        int b,
                                        [Display(2, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaaaaaaaaaa
                                    ) { }
                }
                }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            class D16 {
                                class D20 {
                                    void M(
                                        int b,
                                        [Display(2, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                                        string aaaaaaaaaaaaa
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "d8w20i24_113" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            class D16
                            {
                                class D20
                                {
                                    void M(
                                        int b,
                                        [Display(2, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string aaaaaaaaaaaaa
                                    ) { }
                }
                }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            class D16 {
                                class D20 {
                                    void M(
                                        int b,
                                        [Display(
                                            2,
                                            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                                        )]
                                        string aaaaaaaaaaaaa
                                    ) { }
                                }
                            }
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
    ///     A long enough parameter, by its indent and the attribute's name, keeps the section whole at every width.
    /// </summary>
    [Theory]
    [InlineData("i31i8_120")]
    [InlineData("i27i16_120")]
    [InlineData("j17w23i24_120")]
    [InlineData("a2w23i24_120")]
    [InlineData("O2i8_w30_120")]
    public void NeverChopsPastTheSecondCondition(string cell) {
        var (source, expected) = cell switch {
            "i31i8_120" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] int aaaaaaaaaaaaaaaaaaaaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
                        int aaaaaaaaaaaaaaaaaaaaaaaaaaa
                    ) { }
                }
                """
            ),
            "i27i16_120" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            void M(
                                int b,
                                [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] int aaaaaaaaaaaaaaaaaaaaaaa
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
                                [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
                                int aaaaaaaaaaaaaaaaaaaaaaa
                            ) { }
                        }
                    }
                }
                """
            ),
            "j17w23i24_120" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            class D16
                            {
                                class D20
                                {
                                    void M(
                                        int b,
                                        [JsonPropertyName("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 3)] string aaaaaaaaaaaaaaaa
                                    ) { }
                }
                }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            class D16 {
                                class D20 {
                                    void M(
                                        int b,
                                        [JsonPropertyName("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 3)]
                                        string aaaaaaaaaaaaaaaa
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "a2w23i24_120" => (
                """
                class C
                {
                    class D8
                    {
                        class D12
                        {
                            class D16
                            {
                                class D20
                                {
                                    void M(
                                        int b,
                                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 1)] string aaaaaaaaaaaaaaaa
                                    ) { }
                }
                }
                }
                }
                }
                """,
                """
                class C {
                    class D8 {
                        class D12 {
                            class D16 {
                                class D20 {
                                    void M(
                                        int b,
                                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 1)]
                                        string aaaaaaaaaaaaaaaa
                                    ) { }
                                }
                            }
                        }
                    }
                }
                """
            ),
            "O2i8_w30_120" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] int aaaaaaaaaaaaaaaaaaaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
                        int aaaaaaaaaaaaaaaaaaaaaaaaaa
                    ) { }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }

    /// <summary>
    ///     A named argument and a one-argument section are not this rule's, and keep their own fit.
    /// </summary>
    [Theory]
    [InlineData("Ns15i8_115")]
    [InlineData("As17i8_118")]
    public void LeavesNamedArgumentsAndOneArgumentSectionsAlone(string cell) {
        var (source, expected) = cell switch {
            "Ns15i8_115" => (
                """
                class C
                {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DiagnosticId = "X")] string? aaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [Obsolete("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", DiagnosticId = "X")]
                        string? aaaaaaa
                    ) { }
                }
                """
            ),
            "As17i8_118" => (
                """
                class C
                {
                    void M(
                        int b,
                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] string? aaaaaaaaa
                    ) { }
                }
                """,
                """
                class C {
                    void M(
                        int b,
                        [A("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
                        string? aaaaaaaaa
                    ) { }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
