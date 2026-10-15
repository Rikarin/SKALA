using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #603 (SK-DIV-0571): an author's line break inside a parameter behind its one attribute section leaves the
///     section rules out, on both passes. Every expected string is jb cleanupcode 2025.2.6's own output for the input.
/// </summary>
public sealed class AuthorBrokenParameterSectionIssue603Tests {
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
    ///     The author broke the default's =: the arguments keep their own fit, where the rule measured through the break
    ///     would chop them.
    /// </summary>
    [Theory]
    [InlineData("j2_eq_f102_111")]
    [InlineData("j2_eq_f134_119")]
    public void AnAuthorsBreakAfterTheEqualsIsHonoured(string cell) {
        var (source, expected) = cell switch {
            "j2_eq_f102_111" => (
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [Lqclyc("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefgh", "x")] bool cdpg =
                                    1.5
                            ) { }
                        }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        class D1 {
                            void M(
                                int b,
                                [Lqclyc("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefgh", "x")]
                                bool cdpg =
                                    1.5
                            ) { }
                        }
                    }
                }
                """
            ),
            "j2_eq_f134_119" => (
                """
                class C {
                    class D0 {
                        void M(
                            int b,
                            [Lehurey(null, Kind.A, "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghi", loehqq: 1)] Mode xexyk =
                                0L
                        ) { }
                    }
                }
                """,
                """
                class C {
                    class D0 {
                        void M(
                            int b,
                            [Lehurey(null, Kind.A, "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghi", loehqq: 1)]
                            Mode xexyk =
                                0L
                        ) { }
                    }
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
