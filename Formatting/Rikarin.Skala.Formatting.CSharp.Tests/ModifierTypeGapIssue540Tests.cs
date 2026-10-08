using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #540, SK-DIV-0127: the gap between a field&apos;s modifiers and its type. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class ModifierTypeGapIssue540Tests {
    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
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
    ///     #540: the gap between a field&apos;s modifiers and its type breaks when the type does not fit after them, from
    ///     the type&apos;s 121st column; the name follows by its own gap.
    /// </summary>
    [Fact]
    public void ATypeThatDoesNotFitAfterTheModifiers_MovesBelowThem() {
        Agrees(
            """
            class C {
                public static readonly System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> Overflowing;
            }
            """,
            """
            class C {
                public static readonly
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>>
                    Overflowing;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> Overflowing = null;
            }
            """,
            """
            class C {
                public static readonly
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>>
                    Overflowing = null;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>, System.Collections.Generic.IReadOnlyList<int>> Overflowing;
            }
            """,
            """
            class C {
                public static readonly
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>,
                        System.Collections.Generic.IReadOnlyList<int>> Overflowing;
            }
            """
        );
        Agrees(
            """
            class C {
                [Obsolete] public static readonly System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> Overflowing;
            }
            """,
            """
            class C {
                [Obsolete]
                public static readonly
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>>
                    Overflowing;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly int
                    Short;
            }
            """,
            """
            class C {
                public static readonly int
                    Short;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly
                    int Short;
            }
            """,
            """
            class C {
                public static readonly
                    int Short;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly Dictionary<string, int> NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN;
            }
            """,
            """
            class C {
                public static readonly Dictionary<string, int>
                    NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNNN;
            }
            """,
            """
            class C {
                public static readonly Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>>
                    NNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNNN;
            }
            """,
            """
            class C {
                public static readonly
                    Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNNNNNNNN;
            }
            """,
            """
            class C {
                public static readonly Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>>
                    NNNNNNNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNNNNNNNN;
            }
            """,
            """
            class C {
                public static readonly
                    Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNNNNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNNNNNNNNNNNNN;
            }
            """,
            """
            class C {
                public static readonly
                    Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>>
                    NNNNNNNNNNNNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN;
            }
            """,
            """
            class C {
                public static readonly Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>>
                    NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>> NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN;
            }
            """,
            """
            class C {
                public static readonly
                    Dictionary<string, List<Sxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx>>
                    NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                public static readonly System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<S>> Overflowing;
            }
            """,
            """
            class C {
                public static readonly
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<S>> Overflowing;
            }
            """
        );
    }
}
