namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #602: a ternary whose condition is a type test the author broke before <c>as</c> or <c>is T</c> puts
///     its <c>?</c> and <c>:</c> on the column of the keyword's line, as it does under a broken <c>is null</c>,
///     where a broken <c>== null</c> takes them a level further. Every expected string is <c>jb cleanupcode</c>
///     2025.2.6's own output, measured 2026-10-10 with <c>Testing ask</c>.
/// </summary>
public sealed class TernaryAfterABrokenTypeTestIssue602Tests {
    /// <summary>After <c>return</c>, <c>var x =</c> and as an argument; a written-flat condition; the <c>==</c> control.</summary>
    [Fact]
    public void TheSignsSitOnTheKeywordsColumn() =>
        Oracle.Agrees(
            """
            class C {
                object A() {
                    return model.Get(a, b, c)
                        as IMethodSymbol
                        ? 1
                        : 2;
                }

                object B() {
                    return model.Get(a, b, c)
                        is null
                        ? 1
                        : 2;
                }

                object C2() {
                    return model.Get(a, b, c)
                        is IMethodSymbol
                        ? 1
                        : 2;
                }

                object D() {
                    return (model.Get(a, b, c)
                        as IMethodSymbol)
                        ? 1
                        : 2;
                }

                object E() {
                    var x = model.Get(a, b, c)
                        as IMethodSymbol
                        ? 1
                        : 2;
                    return x;
                }

                object G() {
                    return model.GetSpeculativeSymbolInfo(initializer.SpanStart, creationExpressionValue) as IMethodSymbol ? firstBranchValue : secondBranchValue;
                }

                object H() {
                    return source.Select(alphaValue)
                        .Where(betaValue) as IMethodSymbol ? 1 : 2;
                }

                object I() {
                    Use(model.Get(a, b, c)
                        as IMethodSymbol
                        ? 1
                        : 2);
                    return null;
                }

                object J() {
                    return model.GetSpeculativeSymbolInfo(initializer.SpanStart, creationExpressionValue) is IMethodSymbol ? firstBranchValue : secondBranchValue;
                }

                object F() {
                    return model.Get(a, b, c)
                        == null
                        ? 1
                        : 2;
                }
            }
            """,
            """
            class C {
                object A() {
                    return model.Get(a, b, c)
                        as IMethodSymbol
                        ? 1
                        : 2;
                }

                object B() {
                    return model.Get(a, b, c)
                        is null
                        ? 1
                        : 2;
                }

                object C2() {
                    return model.Get(a, b, c)
                        is IMethodSymbol
                        ? 1
                        : 2;
                }

                object D() {
                    return (model.Get(a, b, c)
                        as IMethodSymbol)
                        ? 1
                        : 2;
                }

                object E() {
                    var x = model.Get(a, b, c)
                        as IMethodSymbol
                        ? 1
                        : 2;
                    return x;
                }

                object G() {
                    return model.GetSpeculativeSymbolInfo(initializer.SpanStart, creationExpressionValue) as IMethodSymbol
                        ? firstBranchValue
                        : secondBranchValue;
                }

                object H() {
                    return source.Select(alphaValue)
                        .Where(betaValue) as IMethodSymbol
                        ? 1
                        : 2;
                }

                object I() {
                    Use(
                        model.Get(a, b, c)
                            as IMethodSymbol
                            ? 1
                            : 2
                    );
                    return null;
                }

                object J() {
                    return model.GetSpeculativeSymbolInfo(initializer.SpanStart, creationExpressionValue) is IMethodSymbol
                        ? firstBranchValue
                        : secondBranchValue;
                }

                object F() {
                    return model.Get(a, b, c)
                        == null
                            ? 1
                            : 2;
                }
            }
            """
        );
}
