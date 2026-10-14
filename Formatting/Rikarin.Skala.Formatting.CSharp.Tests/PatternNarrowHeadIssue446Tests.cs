using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #446 item 2, SK-DIV-0211: a local's <c>=</c> under twelve columns before <c>operand is A or B</c> past the
///     margin. Each pair is the first line end that breaks the <c>=</c> and the column before it, which breaks inside
///     the pattern. Every expected string is jb cleanupcode 2025.2.6's own output for the input, from the 2026-10-14
///     sweeps (<see cref="EqualsFloor.BreaksBeforeAPattern" />).
/// </summary>
public sealed class PatternNarrowHeadIssue446Tests {
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
    ///     A head of 8 at indent 16 before a pattern of 25 breaks the <c>=</c> from 123, where the per-head table broke it
    ///     at every end; a head of 7 before <c>not null and not G</c> from 125; and a head of 10 behind a first
    ///     alternative of nine keeps the pattern on the declaration's line.
    /// </summary>
    [Theory]
    [InlineData("h8_or25_i16_123")]
    [InlineData("h8_or25_i16_122")]
    [InlineData("h7_not23_i8_125")]
    [InlineData("h7_not23_i8_124")]
    [InlineData("h10_or9_40_i24_125")]
    public void TheEqualsBreaksByThePatternTheFirstAlternativeAndTheIndent(string cell) {
        var (source, expected) = Cells[cell];
        Agrees(source, expected);
    }

    static readonly Dictionary<string, (string Source, string Expected)> Cells = new() {
        ["h8_or25_i16_123"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Alpha or BBBBBBBBBBBBBBBB;
                        }
                    }
                }
            }
            """,
            """
            class C {
                object M() {
                    {
                        {
                            bool c =
                                oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Alpha or BBBBBBBBBBBBBBBB;
                        }
                    }
                }
            }
            """
        ),
        ["h8_or25_i16_122"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Alpha or BBBBBBBBBBBBBBBB;
                        }
                    }
                }
            }
            """,
            """
            class C {
                object M() {
                    {
                        {
                            bool c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Alpha
                                or BBBBBBBBBBBBBBBB;
                        }
                    }
                }
            }
            """
        ),
        ["h7_not23_i8_125"] = (
            """
            class C
            {
                object M()
                {
                    var c = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is not null and not GGGGGG;
                }
            }
            """,
            """
            class C {
                object M() {
                    var c =
                        ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is not null
                            and not GGGGGG;
                }
            }
            """
        ),
        ["h7_not23_i8_124"] = (
            """
            class C
            {
                object M()
                {
                    var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is not null and not GGGGGG;
                }
            }
            """,
            """
            class C {
                object M() {
                    var c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is not null
                        and not GGGGGG;
                }
            }
            """
        ),
        ["h10_or9_40_i24_125"] = (
            """
            class C
            {
                object M()
                {
                    {
                        {
                            {
                                {
                                    bool ccc = ooooooooooooooooooooooooooooooooooooooooooooo is Alphaaaaa or BBBBBBBBBBBBBBBBBBBBBBBBBBB;
                                }
                            }
                        }
                    }
                }
            }
            """,
            """
            class C {
                object M() {
                    {
                        {
                            {
                                {
                                    bool ccc = ooooooooooooooooooooooooooooooooooooooooooooo is Alphaaaaa
                                        or BBBBBBBBBBBBBBBBBBBBBBBBBBB;
                                }
                            }
                        }
                    }
                }
            }
            """
        ),
    };
}
