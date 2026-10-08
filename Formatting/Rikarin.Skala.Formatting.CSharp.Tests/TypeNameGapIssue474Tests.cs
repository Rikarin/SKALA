using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #474, SK-DIV-0127: the gap between a field's or a local's type and its name. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class TypeNameGapIssue474Tests {
    const string Long1 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<string>> NNNNNNNNN;";

    const string Long2 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<string>> NNNNNNNNNN;";

    const string Long3 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<string>> NNNNNNNNNNN;";

    const string Long4 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<string>> NNNNNNNNNNNNN;";

    const string Long5 = "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT"
        + "TTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aa"
        + "aaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaa"
        + "aaaa];";

    const string Long6 = "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT"
        + "TTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, a"
        + "aaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaa"
        + "aaaaa];";

    const string Long7 = "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT"
        + "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,"
        + " aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaa"
        + "aaaaaaa];";

    const string Long8 = "Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT"
        + "TTTTTTTTTTTTTTTTTTTTT, int> v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aa"
        + "aaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaa"
        + "aaaa];";

    const string Long9 = "Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT"
        + "TTTTTTTTTTTTTTTTTTTTTT, int> v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, a"
        + "aaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaa"
        + "aaaaa];";

    const string Long10 = "Dictionary<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT"
        + "TTTTTTTTTTTTTTTTTTTTTTTTT, int> v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa"
        + ", aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaa"
        + "aaaaaaaa];";

    const string Long11 = "(Dictionary<Guid, List<Guid>> First, StringBuilder Second, Dictionary<Guid, List"
        + "<Guid>> Third, StringBuilder Fourth) field;";

    const string Long12 = "(Dictionary<Guid, List<Guid>> First, StringBuilder Second, Dictionary<Guid, List"
        + "<Guid>> Third, StringBuilder Fourth)";

    const string Long13 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<string>> Overflowing;";

    const string Long14 = "Dictionary<string, List<int>> mapping = new Dictionary<string, List<int>>(String"
        + "Comparer.OrdinalIgnoreCase);";

    const string Long15 = "private readonly Dictionary<string, List<int>> _mapping = new Dictionary<string,"
        + " List<int>>(StringComparer.OrdinalIgnoreCase);";

    const string Long16 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<string>> a, b;";

    const string Long17 = "event System.EventHandler<System.Collections.Generic.IReadOnlyDictionary<string,"
        + " System.Collections.Generic.IReadOnlyList<string>>> Changed;";

    const string Long18 = "System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generi"
        + "c.IReadOnlyList<string>> local;";

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
    ///     #474: a field or a local whose line through its name and = does not fit breaks between the type and the
    ///     name, the type whole, from 121 columns; at 120 it stays.
    /// </summary>
    [Fact]
    public void PastTheMargin_TheNameMovesBelowTheType() {
        Agrees(
            $$"""
              class C {
                  {{Long1}}
              }
              """,
            $$"""
              class C {
                  {{Long1}}
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long2}}
              }
              """,
            $$"""
              class C {
                  {{Long2}}
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long3}}
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
            $$"""
              class C {
                  {{Long4}}
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
            $$"""
              class C {
                  void M() {
                      {{Long5}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      {{R('T', 107)}} v9 = [
                          aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                          aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                      ];
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long6}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      {{R('T', 108)}}
                          v9 = [
                              aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                              aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                          ];
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long7}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      {{R('T', 110)}}
                          v9 = [
                              aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                              aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                          ];
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long8}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      Dictionary<{{R('T', 90)}}, int> v9 = [
                          aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                          aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                      ];
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long9}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      Dictionary<{{R('T', 91)}}, int>
                          v9 = [
                              aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                              aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                          ];
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long10}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      Dictionary<{{R('T', 94)}}, int>
                          v9 = [
                              aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa,
                              aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa
                          ];
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{R('T', 109)}} Name = Compute(alpha, beta);
              }
              """,
            $$"""
              class C {
                  {{R('T', 109)}} Name =
                      Compute(alpha, beta);
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{R('T', 110)}} Name = Compute(alpha, beta);
              }
              """,
            $$"""
              class C {
                  {{R('T', 110)}}
                      Name = Compute(alpha, beta);
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{R('T', 113)}} Name = Compute(alpha, beta);
              }
              """,
            $$"""
              class C {
                  {{R('T', 113)}}
                      Name = Compute(alpha, beta);
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long11}}
              }
              """,
            $$"""
              class C {
                  {{Long12}}
                      field;
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      Dictionary<{{R('T', 92)}}, int> v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa];
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      Dictionary<{{R('T', 92)}}, int>
                          v9 = [aaaaaaaaaaaaaaaaaaaa, aaaaaaaaaaaaaaaaaaaa];
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long13}}
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
            $$"""
              class C {
                  void M() {
                      {{R('T', 94)}} v9 = [1, 2, 3];
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      {{R('T', 94)}} v9 = [1, 2, 3];
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{R('T', 96)}} v9 = [1, 2, 3];
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      {{R('T', 96)}} v9 = [1, 2, 3];
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{R('T', 98)}} v9 = [1, 2, 3];
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      {{R('T', 98)}} v9 =
                          [1, 2, 3];
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long14}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      {{Long14}}
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long15}}
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
            $$"""
              class C {
                  void M() {
                      {{Long16}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      {{Long16}}
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  {{Long17}}
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
            $$"""
              class C {
                  void M() {
                      const string SomeVeryLongConstantNameThatGoesOnAndOn{{R('X', 44)}} = "value";
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      const string SomeVeryLongConstantNameThatGoesOnAndOn{{R('X', 44)}} = "value";
                  }
              }
              """
        );
        Agrees(
            $$"""
              class C {
                  void M() {
                      {{Long18}}
                  }
              }
              """,
            $$"""
              class C {
                  void M() {
                      {{Long18}}
                  }
              }
              """
        );
    }
}
