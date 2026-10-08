using static Rikarin.Skala.Formatting.CSharp.Tests.TestText;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #530: a conditional whose condition spans lines as a chain or an argument list puts its
///     signs one level past the statement; a broken binary condition puts them one past its operators.
///     Every expected string is <c>jb cleanupcode</c>'s own output, measured 2026-10-08 with
///     <c>Testing ask</c>; <c>constructs/breaks/ternary-after-a-chopped-condition.cs</c> holds the wider set.
/// </summary>
/// <remarks>
///     ⚠ The arms' scope was opened after the condition, on its last line, so the one-level-per-line
///     collapse never met the statement's own level. It now opens on the condition's first line,
///     except under a binary condition — the control below — and a chain headed by a parenthesis.
/// </remarks>
public sealed class TernaryAfterAChoppedConditionIssue530Tests {
    const string Long1 = "var t = someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n"
        + ".Name).WhereSomething(x => x.IsEnabledAndReady) ? otherFallbackValueName.SomeFal"
        + "lbackProperty : third;";

    const string Long2 = "return Compute(alphaaaaaaaaaaaaaaaaaaaaaaaaaaa, betaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        + "aaaaaa, gammaaaaaaaaaaaaaaaaaaaaaaaaaaaaa) ? otherFallbackValueName.SomeFallback"
        + "Property : third;";

    const string Long3 = "var b1 = someParticularThingWithALongNameeeeeeeeeeeeeeee && otherParticularThing"
        + "WithALongNameeeeeeeeeeeeeeeeeeee && thirdddddd ? otherFallbackValueName.SomeFall"
        + "backProperty : third;";

    [Fact]
    public void AChoppedCallOrChain_PutsTheSignsAtTheStatementsLevel() =>
        Oracle.Agrees(
            $$"""
              class T {
                  object M() {
                      {{Long1}}
                      {{Long2}}
                  }
              }
              """,
            """
            class T {
                object M() {
                    var t = someParticularThingWithALongName.SelfLink()
                        .SelfLink()
                        .SelectName(n => n.Name)
                        .WhereSomething(x => x.IsEnabledAndReady)
                        ? otherFallbackValueName.SomeFallbackProperty
                        : third;
                    return Compute(
                        alphaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                        betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                        gammaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                    )
                        ? otherFallbackValueName.SomeFallbackProperty
                        : third;
                }
            }
            """
        );

    /// <summary>The control: a broken binary condition, its signs a level past the operators.</summary>
    [Fact]
    public void ABinaryCondition_PutsTheSignsPastItsOperators() =>
        Oracle.Agrees(
            $$"""
              class T {
                  object M() {
                      {{Long3}}
                  }
              }
              """,
            """
            class T {
                object M() {
                    var b1 = someParticularThingWithALongNameeeeeeeeeeeeeeee
                        && otherParticularThingWithALongNameeeeeeeeeeeeeeeeeeee
                        && thirdddddd
                            ? otherFallbackValueName.SomeFallbackProperty
                            : third;
                }
            }
            """
        );
}
