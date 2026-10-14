namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #602: a sole lambda's whole body that is a type test the author broke before its keyword stacks the
///     test's level only while its operand's argument list breaks — <c>Use(x =&gt; call(</c> / the arguments two levels
///     in / <c>)</c> / <c>is T</c> — and keeps #445's one level past the line when the call stays whole. Every expected
///     string is <c>jb cleanupcode</c> 2025.2.6's own output, from group S's #597 grid.
/// </summary>
public sealed class SoleLambdaBrokenTypeTestIssue602Tests {
    /// <summary>The long call chops and the test stacks; the short call stays whole and the test does not.</summary>
    [Fact]
    public void StacksOnlyWhileTheArgumentsBreak() =>
        Oracle.Agrees(
            """
            class C {
                void Lambda1() { Use(x => model.GetSpeculativeSymbolInfo(initializer.SpanStart, creationExpressionValue, SpeculativeBindingOption.BindAsExpressionValue)
            is not IMethodSymbol creator); }
                void Lambda4() { Use(x => model.GetSpeculativeSymbolInfo(initializer.SpanStart, creationExpressionValue, SpeculativeBindingOption.BindAsExpressionValue)
            as IMethodSymbol); }
                void Lambda8() { Use(x => model.GetSpeculativeSymbolInfo(initializer.SpanStart, creationExpressionValue, SpeculativeBindingOption.BindAsExpressionValue).Symbol
            is not IMethodSymbol creator); }
                void Lambda12() { Use(x => model.GetSpeculativeSymbolInfo(initializer.SpanStart, creationExpressionValue, SpeculativeBindingOption.BindAsExpressionValue).Symbol
            is null); }
                void Lambda15() { Use(x => model.GetSpeculativeSymbolInfo(a, b, c)
            is not IMethodSymbol creator); }
                void Lambda22() { Use(x => model.GetSpeculativeSymbolInfo(a, b, c).Symbol
            is not IMethodSymbol creator); }
            }
            """,
            """
            class C {
                void Lambda1() {
                    Use(x => model.GetSpeculativeSymbolInfo(
                            initializer.SpanStart,
                            creationExpressionValue,
                            SpeculativeBindingOption.BindAsExpressionValue
                        )
                        is not IMethodSymbol creator
                    );
                }

                void Lambda4() {
                    Use(x => model.GetSpeculativeSymbolInfo(
                            initializer.SpanStart,
                            creationExpressionValue,
                            SpeculativeBindingOption.BindAsExpressionValue
                        )
                        as IMethodSymbol
                    );
                }

                void Lambda8() {
                    Use(x => model.GetSpeculativeSymbolInfo(
                                initializer.SpanStart,
                                creationExpressionValue,
                                SpeculativeBindingOption.BindAsExpressionValue
                            )
                            .Symbol
                        is not IMethodSymbol creator
                    );
                }

                void Lambda12() {
                    Use(x => model.GetSpeculativeSymbolInfo(
                                initializer.SpanStart,
                                creationExpressionValue,
                                SpeculativeBindingOption.BindAsExpressionValue
                            )
                            .Symbol
                        is null
                    );
                }

                void Lambda15() {
                    Use(x => model.GetSpeculativeSymbolInfo(a, b, c)
                        is not IMethodSymbol creator
                    );
                }

                void Lambda22() {
                    Use(x => model.GetSpeculativeSymbolInfo(a, b, c).Symbol
                        is not IMethodSymbol creator
                    );
                }
            }
            """
        );
}
