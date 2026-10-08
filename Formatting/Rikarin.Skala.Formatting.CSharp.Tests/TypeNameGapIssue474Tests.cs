using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #474, SK-DIV-0127: the gap between a field's or a local's type and its name. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class TypeNameGapIssue474Tests {
    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
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
    ///     #474: a field or a local whose line through its name and = does not fit breaks between the type and the name,
    ///     the type whole, from 121 columns; at 120 it stays.
    /// </summary>
    [Fact]
    public void PastTheMargin_TheNameMovesBelowTheType() {
        Agrees(
            """
            class C {
                System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> NNNNNNNNN;
            }
            """,
            """
            class C {
                System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> NNNNNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> NNNNNNNNNN;
            }
            """,
            """
            class C {
                System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> NNNNNNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> NNNNNNNNNNN;
            }
            """,
            """
            class C {
                System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>>
                    NNNNNNNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> NNNNNNNNNNNNN;
            }
            """,
            """
            class C {
                System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>>
                    NNNNNNNNNNNNN;
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa];
                }
            }
            """,
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [
                        aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                        aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                    ];
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa];
                }
            }
            """,
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT
                        v9 = [
                            aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                            aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                        ];
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa];
                }
            }
            """,
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT
                        v9 = [
                            aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                            aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                        ];
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa];
                }
            }
            """,
            """
            class C {
                void M() {
                    Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> v9 = [
                        aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                        aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                    ];
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa];
                }
            }
            """,
            """
            class C {
                void M() {
                    Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int>
                        v9 = [
                            aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                            aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                        ];
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa];
                }
            }
            """,
            """
            class C {
                void M() {
                    Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int>
                        v9 = [
                            aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                            aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                        ];
                }
            }
            """
        );
        Agrees(
            """
            class C {
                TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT Name = Compute(alpha, beta);
            }
            """,
            """
            class C {
                TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT Name =
                    Compute(alpha, beta);
            }
            """
        );
        Agrees(
            """
            class C {
                TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT Name = Compute(alpha, beta);
            }
            """,
            """
            class C {
                TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT
                    Name = Compute(alpha, beta);
            }
            """
        );
        Agrees(
            """
            class C {
                TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT Name = Compute(alpha, beta);
            }
            """,
            """
            class C {
                TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT
                    Name = Compute(alpha, beta);
            }
            """
        );
        Agrees(
            """
            class C {
                (Dictionary<Guid, List<Guid>> First, StringBuilder Second, Dictionary<Guid, List<Guid>> Third, StringBuilder Fourth) field;
            }
            """,
            """
            class C {
                (Dictionary<Guid, List<Guid>> First, StringBuilder Second, Dictionary<Guid, List<Guid>> Third, StringBuilder Fourth)
                    field;
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa];
                }
            }
            """,
            """
            class C {
                void M() {
                    Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int>
                        v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa];
                }
            }
            """
        );
        Agrees(
            """
            class C {
                System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> Overflowing;
            }
            """,
            """
            class C {
                System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>>
                    Overflowing;
            }
            """
        );
    }

    /// <summary>
    ///     The gap is not taken when the line through the = fits, when the value is what overflows, for a kept break,
    ///     several declarators, an event field and a constant.
    /// </summary>
    [Fact]
    public void WhenTheHeadFits_TheEqualsOrTheValueDecides() {
        Agrees(
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [1, 2, 3];
                }
            }
            """,
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [1, 2, 3];
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [1, 2, 3];
                }
            }
            """,
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [1, 2, 3];
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [1, 2, 3];
                }
            }
            """,
            """
            class C {
                void M() {
                    TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 =
                        [1, 2, 3];
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    Dictionary<string, List<int>> mapping = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                }
            }
            """,
            """
            class C {
                void M() {
                    Dictionary<string, List<int>> mapping = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                }
            }
            """
        );
        Agrees(
            """
            class C {
                private readonly Dictionary<string, List<int>> _mapping = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            }
            """,
            """
            class C {
                private readonly Dictionary<string, List<int>> _mapping =
                    new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    int
                        a = 1;
                }
            }
            """,
            """
            class C {
                void M() {
                    int
                        a = 1;
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> a, b;
                }
            }
            """,
            """
            class C {
                void M() {
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> a, b;
                }
            }
            """
        );
        Agrees(
            """
            class C {
                event System.EventHandler<System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>>> Changed;
            }
            """,
            """
            class C {
                event System.EventHandler<System.Collections.Generic.IReadOnlyDictionary<string,
                    System.Collections.Generic.IReadOnlyList<string>>> Changed;
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    const string SomeVeryLongConstantNameThatGoesOnAndOnXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX = "value";
                }
            }
            """,
            """
            class C {
                void M() {
                    const string SomeVeryLongConstantNameThatGoesOnAndOnXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX = "value";
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> local;
                }
            }
            """,
            """
            class C {
                void M() {
                    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> local;
                }
            }
            """
        );
    }
}
