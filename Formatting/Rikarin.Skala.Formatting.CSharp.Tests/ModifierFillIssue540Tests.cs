using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #540, SK-DIV-0127: a field's generic type past the margin fills on its modifiers' line, rather than moving
///     below them, once the line it would make below them is long enough. Every expected string is jb cleanupcode
///     2025.2.6's own output for the input, from the 2026-10-10 sweeps.
/// </summary>
public sealed class ModifierFillIssue540Tests {
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
    ///     The type fills on the modifiers' line from the measured length and moves below them a column short of it, for
    ///     any modifiers and at indents 4 and 8.
    /// </summary>
    [Theory]
    [InlineData("H42L1_131")]
    [InlineData("H42L1_132")]
    [InlineData("H57L5_135")]
    [InlineData("H57L5_136")]
    [InlineData("pis8ch52L1_131")]
    [InlineData("pis8ch52L1_132")]
    [InlineData("ir8ch34L3_126")]
    [InlineData("ir8ch34L3_128")]
    [InlineData("pvs4fh43L6_130")]
    [InlineData("pr4H65L1_130")]
    public void FillsOnTheModifiersLineFromTheMeasuredLength(string cell) {
        var (source, expected) = cell switch {
            "H42L1_131" => (
                """
                class C
                {
                    public static readonly Dictionary<KKK, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> N;
                }
                """,
                """
                class C {
                    public static readonly
                        Dictionary<KKK, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> N;
                }
                """
            ),
            "H42L1_132" => (
                """
                class C
                {
                    public static readonly Dictionary<KKK, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> N;
                }
                """,
                """
                class C {
                    public static readonly
                        Dictionary<KKK, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> N;
                }
                """
            ),
            "H57L5_135" => (
                """
                class C
                {
                    public static readonly Dictionary<KKKKKKKKKKKKKKKKKK, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNN;
                }
                """,
                """
                class C {
                    public static readonly
                        Dictionary<KKKKKKKKKKKKKKKKKK, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNN;
                }
                """
            ),
            "H57L5_136" => (
                """
                class C
                {
                    public static readonly Dictionary<KKKKKKKKKKKKKKKKKK, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNN;
                }
                """,
                """
                class C {
                    public static readonly Dictionary<KKKKKKKKKKKKKKKKKK,
                        List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNN;
                }
                """
            ),
            "pis8ch52L1_131" => (
                """
                class C
                {
                    class D
                    {
                        protected internal static ConcurrentDictionary<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKK, IList<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> N;
                    }
                }
                """,
                """
                class C {
                    class D {
                        protected internal static
                            ConcurrentDictionary<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKK, IList<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> N;
                    }
                }
                """
            ),
            "pis8ch52L1_132" => (
                """
                class C
                {
                    class D
                    {
                        protected internal static ConcurrentDictionary<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKK, IList<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> N;
                    }
                }
                """,
                """
                class C {
                    class D {
                        protected internal static ConcurrentDictionary<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKK,
                            IList<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> N;
                    }
                }
                """
            ),
            "ir8ch34L3_126" => (
                """
                class C
                {
                    class D
                    {
                        internal readonly ConcurrentDictionary<KKKKKKKKKKKK, IList<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNN;
                    }
                }
                """,
                """
                class C {
                    class D {
                        internal readonly
                            ConcurrentDictionary<KKKKKKKKKKKK, IList<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNN;
                    }
                }
                """
            ),
            "ir8ch34L3_128" => (
                """
                class C
                {
                    class D
                    {
                        internal readonly ConcurrentDictionary<KKKKKKKKKKKK, IList<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNN;
                    }
                }
                """,
                """
                class C {
                    class D {
                        internal readonly ConcurrentDictionary<KKKKKKKKKKKK,
                            IList<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNN;
                    }
                }
                """
            ),
            "pvs4fh43L6_130" => (
                """
                class C
                {
                    private static Func<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK, Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> NNNNNN;
                }
                """,
                """
                class C {
                    private static Func<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK,
                        Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx> NNNNNN;
                }
                """
            ),
            "pr4H65L1_130" => (
                """
                class C
                {
                    public readonly Dictionary<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> N;
                }
                """,
                """
                class C {
                    public readonly Dictionary<KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK,
                        List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> N;
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }

    /// <summary>
    ///     Behind a head short of the measured floor the type moves below the modifiers however long the line.
    /// </summary>
    [Theory]
    [InlineData("H42L4_138")]
    public void MovesBelowTheModifiersBehindAShortHead(string cell) {
        var (source, expected) = cell switch {
            "H42L4_138" => (
                """
                class C
                {
                    public static readonly Dictionary<KKK, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNN;
                }
                """,
                """
                class C {
                    public static readonly
                        Dictionary<KKK, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNN;
                }
                """
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(cell))
        };

        Agrees(source, expected);
    }
}
