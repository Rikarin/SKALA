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

    /// <summary>
    ///     A property pattern stacks with its operand's arguments, and a chain's links breaking stack the level as an
    ///     argument list's do; the short forms stay one level past the line (group C's probes).
    /// </summary>
    [Fact]
    public void APropertyPatternAndAChainStackToo() =>
        Oracle.Agrees(
            """
            class C {
                void M169() {
                    Apply(x => workspaceIndex.ResolveDeclaredSymbolForNode(currentSyntaxNode, semanticModelForTree, cancellationToken)
            is { IsStatic: true });
                }
                void M349() {
                    Apply(x => compilationContextForTheEntireSolutionWorkspace.GetTheCurrentlySelectedSymbolForTheEditor().Value.ContainingType
            is ITypeSymbol);
                }
                void M487() {
                    Apply(x => loader.Open(path).ResolveDeclaredSymbolForNode(currentSyntaxNode, semanticModelForTree, cancellationToken).Value
            is { IsStatic: true });
                }
                void N13() {
                    Apply(x => index.Lookup(first, second)
            is { Count: > 0 });
                }
                void N169() {
                    Apply(x => a.B(c).D
            is IMethodSymbol);
                }
            }
            """,
            """
            class C {
                void M169() {
                    Apply(x => workspaceIndex.ResolveDeclaredSymbolForNode(
                            currentSyntaxNode,
                            semanticModelForTree,
                            cancellationToken
                        )
                        is { IsStatic: true }
                    );
                }

                void M349() {
                    Apply(x => compilationContextForTheEntireSolutionWorkspace.GetTheCurrentlySelectedSymbolForTheEditor()
                            .Value.ContainingType
                        is ITypeSymbol
                    );
                }

                void M487() {
                    Apply(x => loader.Open(path)
                            .ResolveDeclaredSymbolForNode(currentSyntaxNode, semanticModelForTree, cancellationToken)
                            .Value
                        is { IsStatic: true }
                    );
                }

                void N13() {
                    Apply(x => index.Lookup(first, second)
                        is { Count: > 0 }
                    );
                }

                void N169() {
                    Apply(x => a.B(c).D
                        is IMethodSymbol
                    );
                }
            }
            """
        );
}
