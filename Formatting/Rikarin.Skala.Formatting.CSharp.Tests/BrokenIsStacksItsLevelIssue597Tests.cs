namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #597: a type test the author broke before its <c>is</c> takes a level of its own on top of what
///     is open around it — past an <c>||</c> it is the left operand of, and under it the operand's chain and
///     argument list each take one more. Every expected string is <c>jb cleanupcode</c>'s own output,
///     measured 2026-10-09 with <c>Testing ask</c> on a 308-row grid (if, while, do, else if, return, a
///     local, an assignment, an arrow, a ternary's condition, an argument and a lambda; a long and a short
///     call, with and without <c>.Symbol</c>, <c>is not T t</c>, <c>is T</c>, <c>is null</c> and <c>as T</c>,
///     alone and under <c>||</c> and <c>&amp;&amp;</c>). Found in Skala's own <c>SearchValuesAnalyzer.cs</c>.
/// </summary>
public sealed class BrokenIsStacksItsLevelIssue597Tests {
    const string Call = "model.GetSpeculativeSymbolInfo(initializer.SpanStart, creationExpressionValue, "
        + "SpeculativeBindingOption.BindAsExpressionValue)";

    const string Or = "|| !SymbolEqualityComparer.Default.Equals(creator.ContainingType, factory)";

    /// <summary>Before the fix: the arguments at 12 and the <c>)</c> at 8, flush with <c>return</c>.</summary>
    [Fact]
    public void AloneAfterReturnOrAnEquals_TheArgumentsNestFromTheIssLevel() =>
        Oracle.Agrees(
            $$"""
              class C {
                  object Return1() { return {{Call}}
                  is not IMethodSymbol creator; }
                  void Local1() { var x = {{Call}}
                  is not IMethodSymbol creator; }
              }
              """,
            """
            class C {
                object Return1() {
                    return model.GetSpeculativeSymbolInfo(
                            initializer.SpanStart,
                            creationExpressionValue,
                            SpeculativeBindingOption.BindAsExpressionValue
                        )
                        is not IMethodSymbol creator;
                }

                void Local1() {
                    var x = model.GetSpeculativeSymbolInfo(
                            initializer.SpanStart,
                            creationExpressionValue,
                            SpeculativeBindingOption.BindAsExpressionValue
                        )
                        is not IMethodSymbol creator;
                }
            }
            """
        );

    /// <summary>
    ///     Before the fix: the <c>is</c> on the <c>||</c>'s column or the <c>)</c>'s, and <c>.Symbol</c> and the
    ///     arguments one or two levels short. The <c>if</c> is SearchValuesAnalyzer's own shape.
    /// </summary>
    [Fact]
    public void UnderAnOr_TheIsTheChainAndTheArgumentsEachTakeALevel() =>
        Oracle.Agrees(
            $$"""
              class C {
                  object Return6() { return {{Call}}.Symbol
                  is not IMethodSymbol creator
                  {{Or}}; }
                  object Return10() { return model.GetSpeculativeSymbolInfo(a, b, c)
                  is not IMethodSymbol creator
                  {{Or}}; }
                  void If6() { if ({{Call}}.Symbol
                  is not IMethodSymbol creator
                  {{Or}}) { return; } }
              }
              """,
            """
            class C {
                object Return6() {
                    return model.GetSpeculativeSymbolInfo(
                                    initializer.SpanStart,
                                    creationExpressionValue,
                                    SpeculativeBindingOption.BindAsExpressionValue
                                )
                                .Symbol
                            is not IMethodSymbol creator
                        || !SymbolEqualityComparer.Default.Equals(creator.ContainingType, factory);
                }

                object Return10() {
                    return model.GetSpeculativeSymbolInfo(a, b, c)
                            is not IMethodSymbol creator
                        || !SymbolEqualityComparer.Default.Equals(creator.ContainingType, factory);
                }

                void If6() {
                    if (model.GetSpeculativeSymbolInfo(
                                    initializer.SpanStart,
                                    creationExpressionValue,
                                    SpeculativeBindingOption.BindAsExpressionValue
                                )
                                .Symbol
                            is not IMethodSymbol creator
                        || !SymbolEqualityComparer.Default.Equals(creator.ContainingType, factory)) {
                        return;
                    }
                }
            }
            """
        );

    /// <summary>
    ///     The same in an assignment, an argument and a lambda's <c>||</c> body. A sole lambda's whole body is
    ///     the control: it keeps #445's one level past the line.
    /// </summary>
    [Fact]
    public void InAnAssignmentAnArgumentAndALambda_TheSameStack() =>
        Oracle.Agrees(
            $$"""
              class C {
                  void Assign2() { x = {{Call}}
                  is not IMethodSymbol creator
                  {{Or}}; }
                  void Arg2() { Use({{Call}}
                  is not IMethodSymbol creator
                  {{Or}}); }
                  void Lambda2() { Use(x => {{Call}}
                  is not IMethodSymbol creator
                  {{Or}}); }
                  void Lambda15() { Use(x => model.GetSpeculativeSymbolInfo(a, b, c)
                  is not IMethodSymbol creator); }
              }
              """,
            """
            class C {
                void Assign2() {
                    x = model.GetSpeculativeSymbolInfo(
                                initializer.SpanStart,
                                creationExpressionValue,
                                SpeculativeBindingOption.BindAsExpressionValue
                            )
                            is not IMethodSymbol creator
                        || !SymbolEqualityComparer.Default.Equals(creator.ContainingType, factory);
                }

                void Arg2() {
                    Use(
                        model.GetSpeculativeSymbolInfo(
                                initializer.SpanStart,
                                creationExpressionValue,
                                SpeculativeBindingOption.BindAsExpressionValue
                            )
                            is not IMethodSymbol creator
                        || !SymbolEqualityComparer.Default.Equals(creator.ContainingType, factory)
                    );
                }

                void Lambda2() {
                    Use(x => model.GetSpeculativeSymbolInfo(
                                initializer.SpanStart,
                                creationExpressionValue,
                                SpeculativeBindingOption.BindAsExpressionValue
                            )
                            is not IMethodSymbol creator
                        || !SymbolEqualityComparer.Default.Equals(creator.ContainingType, factory)
                    );
                }

                void Lambda15() {
                    Use(x => model.GetSpeculativeSymbolInfo(a, b, c)
                        is not IMethodSymbol creator
                    );
                }
            }
            """
        );
}
