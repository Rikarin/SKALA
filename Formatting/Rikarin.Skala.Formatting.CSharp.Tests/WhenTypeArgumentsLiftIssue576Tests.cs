namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #576: an arm whose <c>when</c> condition holds a type argument list too long for one line. Under an
///     arrow that breaks, kept or for width, the type arguments' continuation lifts two levels past the arm, as a
///     <c>when</c> list does (#564). A break the author kept after the <c>when</c> stays. Every expected string is
///     <c>jb cleanupcode</c>'s own output, measured 2026-10-10 with <c>Testing ask</c> (SK-DIV-0399).
/// </summary>
public sealed class WhenTypeArgumentsLiftIssue576Tests {
    const string Condition = "Materialise<List<bool>, IReadOnlyDictionary<(int? First, TimeSpan Second), "
        + "(int? First, TimeSpan Second)>>()";

    /// <summary>Before the fix: the type arguments one level past the arm, at 16, under either kept break.</summary>
    [Fact]
    public void UnderABrokenArrow_TheTypeArgumentsLiftTwoLevels() =>
        Oracle.Agrees(
            $$"""
              class C {
                  object M5() {
                      return state switch { DateTime { P25: not null } when {{Condition}}
              => (from item in items where "ssssss" select item), _ => 0 };
                  }

                  object M6() {
                      return state switch { DateTime { P25: not null } when {{Condition}} =>
              (from item in items where "ssssss" select item), _ => 0 };
                  }
              }
              """,
            """
            class C {
                object M5() {
                    return state switch {
                        DateTime { P25: not null } when Materialise<List<bool>,
                                IReadOnlyDictionary<(int? First, TimeSpan Second), (int? First, TimeSpan Second)>>()
                            => (from item in items where "ssssss" select item),
                        _ => 0
                    };
                }

                object M6() {
                    return state switch {
                        DateTime { P25: not null } when Materialise<List<bool>,
                                IReadOnlyDictionary<(int? First, TimeSpan Second), (int? First, TimeSpan Second)>>() =>
                            (from item in items where "ssssss" select item),
                        _ => 0
                    };
                }
            }
            """
        );

    /// <summary>Before the fix: re-joined as <c>when Materialise&lt;List&lt;bool&gt;,</c>, the condition too wide below.</summary>
    [Fact]
    public void AnAuthorsBreakAfterTheWhen_IsKept() =>
        Oracle.Agrees(
            $$"""
              class C {
                  object M1() {
                      return state switch { DateTime { P25: not null } when
              {{Condition}} => (from item in items where "sssssssssssssssssssss" select item), _ => 0 };
                  }
              }
              """,
            """
            class C {
                object M1() {
                    return state switch {
                        DateTime { P25: not null } when
                            Materialise<List<bool>,
                                IReadOnlyDictionary<(int? First, TimeSpan Second), (int? First, TimeSpan Second)>>() =>
                            (from item in items where "sssssssssssssssssssss" select item),
                        _ => 0
                    };
                }
            }
            """
        );
}
