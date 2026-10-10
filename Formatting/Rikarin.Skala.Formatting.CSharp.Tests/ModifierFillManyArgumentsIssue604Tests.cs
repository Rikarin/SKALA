using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #604: a field whose generic type has three or more arguments fills on its modifiers' line by the #540
///     table read at the fill's last comma. Every expected string is jb cleanupcode 2025.2.6's own output for the input,
///     from the 2026-10-10 sweeps.
/// </summary>
public sealed class ModifierFillManyArgumentsIssue604Tests {
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
    ///     Three and four arguments, at the measured length and a column short of it.
    /// </summary>
    [Theory]
    [InlineData("psr_f3_h55_L3_131")]
    [InlineData("psr_f3_h55_L3_132")]
    [InlineData("pr_f4_h25_L1_130")]
    [InlineData("is8_t3_h30_L4_135")]
    [InlineData("ppr4_a4_h19_L6_137")]
    public void FillsByTheLastCommaThatFits(string cell) {
        var (source, expected) = cell switch {
            "psr_f3_h55_L3_131" => (
                """
                class C
                {
                    public static readonly Func<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK, int, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNN;
                }
                """,
                """
                class C {
                    public static readonly
                        Func<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK, int, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNN;
                }
                """
            ),
            "psr_f3_h55_L3_132" => (
                """
                class C
                {
                    public static readonly Func<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK, int, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNN;
                }
                """,
                """
                class C {
                    public static readonly Func<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK, int,
                        List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNN;
                }
                """
            ),
            "pr_f4_h25_L1_130" => (
                """
                class C
                {
                    private readonly Func<KKKKKKKKKKKKKKKKKKK, int, long, Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> N;
                }
                """,
                """
                class C {
                    private readonly Func<KKKKKKKKKKKKKKKKKKK, int, long,
                        Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> N;
                }
                """
            ),
            "is8_t3_h30_L4_135" => (
                """
                class C
                {
                    class D
                    {
                        internal static Tuple<KKKKKKKKKKKKKKKKKKKKKKK, string, IList<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNN;
                    }
                }
                """,
                """
                class C {
                    class D {
                        internal static Tuple<KKKKKKKKKKKKKKKKKKKKKKK, string,
                            IList<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNN;
                    }
                }
                """
            ),
            "ppr4_a4_h19_L6_137" => (
                """
                class C
                {
                    protected readonly Action<KKKKKKKKKKK, int, bool, Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> NNNNNN;
                }
                """,
                """
                class C {
                    protected readonly Action<KKKKKKKKKKK, int, bool,
                        Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> NNNNNN;
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
