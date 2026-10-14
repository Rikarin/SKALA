namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #620: where a sole lambda breaks after its arrow over a type test written on one line, or over a whole body
///     that is a one-argument call. Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output, from a probe
///     written after the rules (2026-10-14), one member kept and one broken of each.
/// </summary>
public sealed class SoleLambdaUnbrokenBodyArrowIssue620Tests {
    [Fact]
    public void TheArrowBreaksByTheBodysRule() =>
        Oracle.Agrees(
            """
            class C {
                object M15() {
                    return registry.Where(x => context.Workspace.CurrentSolution.GetDocument(acontainingNamespacespeculativeModelcompilationUnitan));
                }
                object M22() {
                    await q.Select((Node node) => context.Workspace.CurrentSolution.GetDocument(nodeanalyzerOptionsdocumentationCommentdocumentationCommentanal).First() is not null);
                }
                object M27() {
                    TryFind((Node node) => Analyze(adocumentationCommentcompilationUnitcompilationUnitcancellationTokenanalyzerOptionscompilationUnitcompilat) as INamedTypeSymbol);
                }
                object M33() {
                    r = Do(x => this.semanticModel.GetDeclaredSymbol(acancellationTokencompilationUnitdeclaringSymbolanalyzerOptionsd));
                }
                object M60() {
                    var r = Filter((a, b) => Normalize(nodedeclaringSymbolcancellationTokencompilationUnitconta, node, b, node) is { IsStatic: true });
                }
                object M61() {
                    Run(static x => this.semanticModel.GetDeclaredSymbol(tokenanalyzerOptionscancellationTokenspeculativeModelspeculativeMo, token) as INamedTypeSymbol);
                }
            }
            """,
            """
            class C {
                object M15() {
                    return registry.Where(x =>
                        context.Workspace.CurrentSolution.GetDocument(acontainingNamespacespeculativeModelcompilationUnitan)
                    );
                }

                object M22() {
                    await q.Select((Node node) =>
                        context.Workspace.CurrentSolution
                            .GetDocument(nodeanalyzerOptionsdocumentationCommentdocumentationCommentanal)
                            .First() is not null
                    );
                }

                object M27() {
                    TryFind((Node node) =>
                        Analyze(
                            adocumentationCommentcompilationUnitcompilationUnitcancellationTokenanalyzerOptionscompilationUnitcompilat
                        ) as INamedTypeSymbol
                    );
                }

                object M33() {
                    r = Do(x => this.semanticModel.GetDeclaredSymbol(
                            acancellationTokencompilationUnitdeclaringSymbolanalyzerOptionsd
                        )
                    );
                }

                object M60() {
                    var r = Filter((a, b) =>
                        Normalize(nodedeclaringSymbolcancellationTokencompilationUnitconta, node, b, node) is { IsStatic: true }
                    );
                }

                object M61() {
                    Run(static x => this.semanticModel.GetDeclaredSymbol(
                            tokenanalyzerOptionscancellationTokenspeculativeModelspeculativeMo,
                            token
                        ) as INamedTypeSymbol
                    );
                }
            }
            """
        );
}
