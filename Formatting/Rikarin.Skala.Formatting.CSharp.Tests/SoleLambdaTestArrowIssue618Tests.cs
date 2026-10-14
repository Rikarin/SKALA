namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #618: where a sole lambda breaks after its arrow in front of a type test the author broke before its
///     keyword — by the measured lines for a call of two or more arguments, for a first call of two or more with links
///     after it, and otherwise as soon as the operand overflows beside the arrow. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output, from probes written after the rules (2026-10-14), one member kept and
///     one broken of each.
/// </summary>
public sealed class SoleLambdaTestArrowIssue618Tests {
    [Fact]
    public void TheArrowBreaksByTheOperandsRule() =>
        Oracle.Agrees(
            """
            class C {
                async void M1() {
                    Check((Node n) => Workspace.Current.Services.GetLanguageServiceForTheDocument(first, second, thirdExtraExtraExtraExtraExt)
            is not null);
                }
                async void M4() {
                    result = Schedule(static x => compilation.GetSemanticModelForTheTree(node, cancellationTokenExtraExtraExtraExtraExtraExtraExtraExtraExtraExtr)
            is { IsStatic: true });
                }
                async void M2() {
                    result = X(symbol => analysisContextForTheCurrentDocumentanalysisContextForTheCurrentDocumentanalysisContextFor.factory.Create()
            as INamedTypeSymbol);
                }
                async void M49() {
                    Run((x) => Lookup(someVeryLongIdentifierNamesomeVeryLongIdentifierNamesomeVeryLongIdentifierNamesomeVeryLongIden).Value
            is not null);
                }
                object M26() {
                    yield return Do((a, b) => this.semanticModel.GetDeclaredSymbol(a, bspeculativeSemanticModelcontainingNamespaceOrTypeSy).Result.Value
            is { IsStatic: true });
                }
                object M48() {
                    list.Exists((x) => Analyze(a, bspeculativeSemanticModelspeculativeSemanticModelcontainingNamespaceOrTypeSymbo, c, d).Value
            as INamedTypeSymbol);
                }
            }
            """,
            """
            class C {
                async void M1() {
                    Check((Node n) =>
                        Workspace.Current.Services.GetLanguageServiceForTheDocument(first, second, thirdExtraExtraExtraExtraExt)
                            is not null
                    );
                }

                async void M4() {
                    result = Schedule(static x => compilation.GetSemanticModelForTheTree(
                            node,
                            cancellationTokenExtraExtraExtraExtraExtraExtraExtraExtraExtraExtr
                        )
                        is { IsStatic: true }
                    );
                }

                async void M2() {
                    result = X(symbol =>
                        analysisContextForTheCurrentDocumentanalysisContextForTheCurrentDocumentanalysisContextFor.factory.Create()
                            as INamedTypeSymbol
                    );
                }

                async void M49() {
                    Run((x) => Lookup(
                                someVeryLongIdentifierNamesomeVeryLongIdentifierNamesomeVeryLongIdentifierNamesomeVeryLongIden
                            )
                            .Value
                        is not null
                    );
                }

                object M26() {
                    yield return Do((a, b) =>
                        this.semanticModel.GetDeclaredSymbol(a, bspeculativeSemanticModelcontainingNamespaceOrTypeSy).Result.Value
                            is { IsStatic: true }
                    );
                }

                object M48() {
                    list.Exists((x) => Analyze(
                                a,
                                bspeculativeSemanticModelspeculativeSemanticModelcontainingNamespaceOrTypeSymbo,
                                c,
                                d
                            )
                            .Value
                        as INamedTypeSymbol
                    );
                }
            }
            """
        );
}
