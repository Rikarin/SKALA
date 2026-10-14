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

    /// <summary>
    ///     A call the author broke inside keeps the arrow, in every class; the same call written flat breaks it.
    /// </summary>
    [Fact]
    public void AnOperandTheAuthorBrokeKeepsTheArrow() =>
        Oracle.Agrees(
            """
            public sealed class C {
                public void M1() {
                    var refused = Assert.Throws<InvalidOperationException>(static () => OracleEditorConfig.Reading(
                            Corpus.RepositoryEditorConfigPathAndSomeMoreText
                        )
                    );
                }
                public void M3() {
                    var found = registry.Where(static x => provider.GetServiceOfTheGivenKind(
                            someVeryLongIdentifierNameOfTheServiceTypeToLookUp
                        ) is IMethodSymbol
                    );
                }
                public void M4() {
                    var found = registry.Where(static x => provider.GetServiceOfTheGivenKind(
                            someVeryLongIdentifierNameOfTheServiceType,
                            anotherArgument
                        ) as INamedTypeSymbol
                    );
                }
                public void M5() {
                    var found = registry.Where(static x => provider.GetServiceOfTheGivenKind(
                            someVeryLongIdentifierNameOfTheServiceType,
                            anotherArgument
                        )
                        is IMethodSymbol
                    );
                }
                public void M7() {
                    var refused = Assert.Throws<InvalidOperationException>(static () => OracleEditorConfig.Reading(Corpus.RepositoryEditorConfigPathAndSomeMoreText));
                }
            }
            """,
            """
            public sealed class C {
                public void M1() {
                    var refused = Assert.Throws<InvalidOperationException>(static () => OracleEditorConfig.Reading(
                            Corpus.RepositoryEditorConfigPathAndSomeMoreText
                        )
                    );
                }

                public void M3() {
                    var found = registry.Where(static x => provider.GetServiceOfTheGivenKind(
                            someVeryLongIdentifierNameOfTheServiceTypeToLookUp
                        ) is IMethodSymbol
                    );
                }

                public void M4() {
                    var found = registry.Where(static x => provider.GetServiceOfTheGivenKind(
                            someVeryLongIdentifierNameOfTheServiceType,
                            anotherArgument
                        ) as INamedTypeSymbol
                    );
                }

                public void M5() {
                    var found = registry.Where(static x => provider.GetServiceOfTheGivenKind(
                            someVeryLongIdentifierNameOfTheServiceType,
                            anotherArgument
                        )
                        is IMethodSymbol
                    );
                }

                public void M7() {
                    var refused = Assert.Throws<InvalidOperationException>(static () =>
                        OracleEditorConfig.Reading(Corpus.RepositoryEditorConfigPathAndSomeMoreText)
                    );
                }
            }
            """
        );
}
