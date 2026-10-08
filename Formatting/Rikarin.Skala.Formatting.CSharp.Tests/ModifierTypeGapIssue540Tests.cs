using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #540, SK-DIV-0127: the gap between a field&apos;s modifiers and its type. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class ModifierTypeGapIssue540Tests {
    const string Long1 = "public static readonly System.Collections.Generic.IReadOnlyDictionary<string, Sy"
        + "stem.Collections.Generic.IReadOnlyList<string>> Overflowing;";

    const string Long2 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<string>>";

    const string Long3 = "public static readonly System.Collections.Generic.IReadOnlyDictionary<string, Sy"
        + "stem.Collections.Generic.IReadOnlyList<string>> Overflowing = null;";

    const string Long4 = "public static readonly System.Collections.Generic.IReadOnlyDictionary<string, Sy"
        + "stem.Collections.Generic.IReadOnlyList<string>, System.Collections.Generic.IRead"
        + "OnlyList<int>> Overflowing;";

    const string Long5 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<string>,";

    const string Long6 = "[Obsolete] public static readonly System.Collections.Generic.IReadOnlyDictionary"
        + "<string, System.Collections.Generic.IReadOnlyList<string>> Overflowing;";

    const string Long7 = "public static readonly System.Collections.Generic.IReadOnlyDictionary<string, Sy"
        + "stem.Collections.Generic.IReadOnlyList<S>> Overflowing;";

    const string Long8 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<S>> Overflowing;";

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
    ///     #540: the gap between a field's modifiers and its type breaks when the type does not fit after them, from
    ///     the type&apos;s 121st column; the name follows by its own gap.
    /// </summary>
    [Fact]
    public void ATypeThatDoesNotFitAfterTheModifiers_MovesBelowThem() {
        Agrees(
            $$"""
              class C {
                  {{Long1}}
              }
              """,
            $$"""
              class C {
                  public static readonly
                      {{Long2}}
                      Overflowing;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long3}}
              }
              """,
            $$"""
              class C {
                  public static readonly
                      {{Long2}}
                      Overflowing = null;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long4}}
              }
              """,
            $$"""
              class C {
                  public static readonly
                      {{Long5}}
                          System.Collections.Generic.IReadOnlyList<int>> Overflowing;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long6}}
              }
              """,
            $$"""
              class C {
                  [Obsolete]
                  public static readonly
                      {{Long2}}
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
            $$"""
              class C {
                  public static readonly Dictionary<string, int> {{R('N', 69)}};
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
            $$"""
              class C {
                  public static readonly Dictionary<string, List<S{{R('x', 63)}}>> NNNNNN;
              }
              """,
            $$"""
              class C {
                  public static readonly Dictionary<string, List<S{{R('x', 63)}}>>
                      NNNNNN;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  public static readonly Dictionary<string, List<S{{R('x', 67)}}>> NNNNNN;
              }
              """,
            $$"""
              class C {
                  public static readonly
                      Dictionary<string, List<S{{R('x', 67)}}>> NNNNNN;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  public static readonly Dictionary<string, List<S{{R('x', 66)}}>> NNNNNNNNNNN;
              }
              """,
            $$"""
              class C {
                  public static readonly Dictionary<string, List<S{{R('x', 66)}}>>
                      NNNNNNNNNNN;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  public static readonly Dictionary<string, List<S{{R('x', 70)}}>> NNNNNNNNNNN;
              }
              """,
            $$"""
              class C {
                  public static readonly
                      Dictionary<string, List<S{{R('x', 70)}}>> NNNNNNNNNNN;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  public static readonly Dictionary<string, List<S{{R('x', 69)}}>> NNNNNNNNNNNNNNNN;
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
            $$"""
              class C {
                  public static readonly Dictionary<string, List<S{{R('x', 66)}}>> NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN;
              }
              """,
            $$"""
              class C {
                  public static readonly Dictionary<string, List<S{{R('x', 66)}}>>
                      NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  public static readonly Dictionary<string, List<S{{R('x', 70)}}>> NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN;
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
            $$"""
              class C {
                  {{Long7}}
              }
              """,
            $$"""
              class C {
                  public static readonly
                      {{Long8}}
              }
              """
        );
    }
}
