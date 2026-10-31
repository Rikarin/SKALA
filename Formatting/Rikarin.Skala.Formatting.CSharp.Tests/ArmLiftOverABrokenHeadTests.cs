namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     SK-DIV-0607, the Nightly of 2026-10-31 (fuzz 1037321270251332292 on <c>constructs/breaks/case-label-when.cs</c>):
///     a switch arm whose head spans lines — <c>SomeLongConstant</c> / <c>.Value when …</c> — with the condition and the
///     arrow broken for width. The arm's lift paid the arm's level, and the condition's group, opened on the head's
///     second line, paid it again: the condition two levels past the arm on pass one, one level on pass two, where the
///     arrow's break was read as kept.
/// </summary>
public sealed class ArmLiftOverABrokenHeadTests {
    /// <summary>
    ///     The condition, the <c>when</c> and the <c>=&gt;</c> one level past the arm, as <c>jb cleanupcode</c> writes
    ///     them (measured 2026-10-31 with <c>Testing ask</c>). ⚠ The oracle puts a dot the author broke inside the
    ///     pattern two levels past the arm, <c>.Value when</c> at 20, where Skala writes it at 16 on both passes: a
    ///     separate, stable divergence, recorded in SK-DIV-0607 and pinned here as Skala's answer.
    /// </summary>
    [Fact]
    public void AConditionBelowAHeadThatSpansLines_SitsOneLevelPastTheArm() =>
        SkalaWrites(
            """
            class C {
                int A(object value) {
                    var r = value switch {
                        SomeLongConstant
            .Value when xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx_wwwwwwwwwwwwwwwwwwwwwww => 1,
                        _ => 0
                    };
                    return r;
                }

                int B(object value) {
                    var r = value switch {
                        SomeLongConstant
            .Value
            when xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx_wwwwwwwwwwwwwwwwwwwwwww => 1,
                        _ => 0
                    };
                    return r;
                }

                int P(object value) {
                    var r = value switch {
                        (1,
            2) when Materialise<Guid, StringBuilder, Dictionary<string, List<int>>, IReadOnlyDictionary<decimal, decimal>>() => Body(firstArgumentValue, secondArgumentValue, thirdArgumentValue, fourthArgumentValue, fifthArgumentValue),
                        _ => 0
                    };
                    return r;
                }
            }
            """,
            """
            class C {
                int A(object value) {
                    var r = value switch {
                        SomeLongConstant
                            .Value when
                            xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx_wwwwwwwwwwwwwwwwwwwwwww
                            => 1,
                        _ => 0
                    };
                    return r;
                }

                int B(object value) {
                    var r = value switch {
                        SomeLongConstant
                            .Value
                            when
                            xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx_wwwwwwwwwwwwwwwwwwwwwww
                            => 1,
                        _ => 0
                    };
                    return r;
                }

                int P(object value) {
                    var r = value switch {
                        (1,
                                2) when
                            Materialise<Guid, StringBuilder, Dictionary<string, List<int>>, IReadOnlyDictionary<decimal, decimal>>()
                            => Body(
                                firstArgumentValue,
                                secondArgumentValue,
                                thirdArgumentValue,
                                fourthArgumentValue,
                                fifthArgumentValue
                            ),
                        _ => 0
                    };
                    return r;
                }
            }
            """
        );

    /// <summary>Skala's answer, and a second pass changes nothing.</summary>
    static void SkalaWrites(string source, string expected) {
        var once = Format.Text(source);
        Assert.Equal(expected, once.TrimEnd('\n'));
        Assert.Equal(once, Format.Text(once));
    }
}
