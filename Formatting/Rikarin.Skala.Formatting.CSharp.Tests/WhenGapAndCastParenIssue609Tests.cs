namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Round four of #576, #598 and #609: the gap after an arm's <c>when</c> by its measured table, an anonymous
///     object's braces after a <c>when</c>, and the gap after a cast's <c>)</c> before a parenthesised operator. Every
///     expected string is <c>jb cleanupcode</c>'s own output, measured 2026-10-10 with <c>Testing ask</c>.
/// </summary>
public sealed class WhenGapAndCastParenIssue609Tests {
    static readonly string Type45 = "T" + new string('y', 27);

    static string Condition(int ts) =>
        "Materialise<List<bool>, IReadOnlyDictionary<int, T" + new string('t', ts) + ">>()";

    /// <summary>
    ///     Before a body read through, the <c>when</c> breaks while <c>25·t + 6·h ≤ 3191</c> (the <c>when</c> ending at
    ///     45 here, the line below 116 and 117 columns). Before a wide body it breaks whenever the condition does not fit
    ///     beside it, below or not. Before the fix the first and third broke inside the type arguments, the second
    ///     after the <c>when</c>.
    /// </summary>
    [Fact]
    public void TheWhenGap_FollowsTheMeasuredTable() =>
        Oracle.Agrees(
            $$"""
              class C {
                  object M192() {
                      return state switch { {{Type45}} when {{Condition(40)}} => 1, _ => 0 };
                  }

                  object M193() {
                      return state switch { {{Type45}} when {{Condition(41)}} => 1, _ => 0 };
                  }

                  object M26() {
                      return state switch { DateTime { P25: not null } when {{Condition(54)}} => "{{new string('s', 30)}}", _ => 0 };
                  }
              }
              """,
            $$"""
              class C {
                  object M192() {
                      return state switch {
                          {{Type45}} when
                              {{Condition(40)}} => 1,
                          _ => 0
                      };
                  }

                  object M193() {
                      return state switch {
                          {{Type45}} when Materialise<List<bool>,
                              IReadOnlyDictionary<int, T{{new string('t', 41)}}>>() => 1,
                          _ => 0
                      };
                  }

                  object M26() {
                      return state switch {
                          DateTime { P25: not null } when
                              Materialise<List<bool>,
                                  IReadOnlyDictionary<int, T{{new string('t', 54)}}>>() =>
                              "{{new string('s', 30)}}",
                          _ => 0
                      };
                  }
              }
              """
        );

    /// <summary>
    ///     #609: an anonymous object after a <c>when</c> breaks its braces, and a name with no break point breaks the
    ///     <c>when</c>. Before the fix the first broke the <c>when</c> (pass two then lifted the query) and the second
    ///     kept it beside the name on pass one and broke it on pass two.
    /// </summary>
    [Fact]
    public void AnAnonymousObjectBreaksItsBraces_AndANameTheWhen() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var v212 = value switch {
                        string { P215 : not null } when new { Kind = "sssssssssssssssssss", Count = "sssssssssssssssssssssssssssss" } => (from item in items where 0x79c select "ssssssssss"),
                        _ => 0
                    };
                }

                object N(object owner) =>
                    owner switch {
                        DateTime { P25: not null } when SomeVeryLongIdentifierForTheConditionThatGoesOnAndOnAndOnAndOnAndOn_wwwwwwwww => 1,
                        _ => 0
                    };
            }
            """,
            """
            class C {
                void M() {
                    var v212 = value switch {
                        string { P215 : not null } when new {
                            Kind = "sssssssssssssssssss", Count = "sssssssssssssssssssssssssssss"
                        } => (from item in items where 0x79c select "ssssssssss"),
                        _ => 0
                    };
                }

                object N(object owner) =>
                    owner switch {
                        DateTime { P25: not null } when
                            SomeVeryLongIdentifierForTheConditionThatGoesOnAndOnAndOnAndOnAndOn_wwwwwwwww => 1,
                        _ => 0
                    };
            }
            """
        );

    /// <summary>
    ///     #598: where the operand's <c>)</c> lands one column past the margin and the <c>=</c> cannot take the value,
    ///     a wide cast breaks after its <c>)</c> behind a short first operand (<c>3·s − a ≥ 64</c>). Before the fix
    ///     both broke inside the parentheses.
    /// </summary>
    [Fact]
    public void AWideCastBreaksAfterItsParenthesis() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void M127() {
                      x = (T{{new string('x', 19)}})({{new string('a', 14)}} + {{new string('b', 68)}});
                  }

                  void M128() {
                      x = (T{{new string('x', 19)}})({{new string('a', 16)}} + {{new string('b', 66)}});
                  }
              }
              """,
            $$"""
              class C {
                  void M127() {
                      x = (T{{new string('x', 19)}})
                          ({{new string('a', 14)}} + {{new string('b', 68)}});
                  }

                  void M128() {
                      x = (T{{new string('x', 19)}})({{new string('a', 16)}}
                          + {{new string('b', 66)}});
                  }
              }
              """
        );
}
