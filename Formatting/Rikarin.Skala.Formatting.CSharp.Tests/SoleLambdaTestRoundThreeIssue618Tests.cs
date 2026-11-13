namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #618, SK-DIV-0520 round three: a sole lambda over a type test the author broke before its keyword. Every
///     expected string is <c>jb cleanupcode</c> 2025.2.6's own output, from probe H (seed 74902), written after the
///     rules.
/// </summary>
public sealed class SoleLambdaTestRoundThreeIssue618Tests {
    /// <summary>
    ///     A property chain the test reads fills before the last dot that fits, two levels past the line it starts on,
    ///     the test one; under the arrow broken or kept.
    /// </summary>
    [Fact]
    public void APropertyChain_Fills() =>
        Oracle.Agrees(
            """
            class C {
                object M135() {
                    return TryFind(_ => x.DeltaindiaSymbolLimaoscarHotelm.KiloBravogolfNameB.BravoFoxtrotkiloFoxtrotNovembe.CharlieechoValuealphaHotelgolfNameLimaL
            is not null);
                }
                async void M295() {
                    await q.X(x => deltaHote.JulietindiaSymbolPapaDelt.EchoValueJulietDelta.BravoindiaSymbol.NovemberBravokilogolfNamecharl.OscargolfNameHotelBravoNovem
            as INamedTypeSymbol);
                }
                object M303() {
                    if (c0) { Run((x) => ctx.Hote.OscarkiloLimaechoValuegolfNameHot.KiloFoxtrotkilocha.BravoBravoPapaoscaroscarindia.Novemb
            is SyntaxNode); }
                }
            }
            """,
            """
            class C {
                object M135() {
                    return TryFind(_ =>
                        x.DeltaindiaSymbolLimaoscarHotelm.KiloBravogolfNameB.BravoFoxtrotkiloFoxtrotNovembe
                                .CharlieechoValuealphaHotelgolfNameLimaL
                            is not null
                    );
                }

                async void M295() {
                    await q.X(x =>
                        deltaHote.JulietindiaSymbolPapaDelt.EchoValueJulietDelta.BravoindiaSymbol.NovemberBravokilogolfNamecharl
                                .OscargolfNameHotelBravoNovem
                            as INamedTypeSymbol
                    );
                }

                object M303() {
                    if (c0) {
                        Run((x) => ctx.Hote.OscarkiloLimaechoValuegolfNameHot.KiloFoxtrotkilocha.BravoBravoPapaoscaroscarindia
                                .Novemb
                            is SyntaxNode
                        );
                    }
                }
            }
            """
        );

    /// <summary>A property chain ending in a call of no arguments fills the same way, the call with its property.</summary>
    [Fact]
    public void APropertyChainEndingInANoArgumentCall_Fills() =>
        Oracle.Agrees(
            """
            class C {
                object M174() {
                    Visit(x => deltaLimaDeltacharl.AlphaDeltaJulietoscarPapaindiaS.EchoValueechoValuealphakiloLimaki.LimaindiaSymbolF.Build()
            as INamedTypeSymbol);
                }
            }
            """,
            """
            class C {
                object M174() {
                    Visit(x => deltaLimaDeltacharl.AlphaDeltaJulietoscarPapaindiaS.EchoValueechoValuealphakiloLimaki
                            .LimaindiaSymbolF.Build()
                        as INamedTypeSymbol
                    );
                }
            }
            """
        );

    /// <summary>
    ///     One call of one argument too wide for the line below keeps the arrow when its argument's line reaches
    ///     column 114 and the head before the lambda, with the properties after the call, is narrow against the
    ///     parameters with the call's head.
    /// </summary>
    [Fact]
    public void AOneArgumentCallTooWideBelow_KeepsTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M132() {
                    Analyze((Node node) => model.GetSymbolInfo(kilokiloHotelgolfNamecharlieJulietNovemberLimagolfNameHotelgolfNameDeltamikeNodemikeNodemikeNodemikeNodeFoxtrotoscaralpha).Name
            as INamedTypeSymbol);
                }
                object M340() {
                    _ = this.TryFind((SyntaxNode n) => model.GetSymbolInfo(deltagolfNameJulietkilooscargolfNameDeltaindiaSymbolgolfNameechoValueNovemberDeltaechoValuekilokiloPapaFoxtrotosca)
            is SyntaxNode);
                }
                object M243() {
                    Check(static x => Find(oscaralphaHotelFoxtrotechoValuealphagolfNameechoValuemikeNodegolfNameFoxtrotLimacharliekiloalphaLimachar)
            is { IsStatic: true });
                }
            }
            """,
            """
            class C {
                object M132() {
                    Analyze((Node node) => model.GetSymbolInfo(
                                kilokiloHotelgolfNamecharlieJulietNovemberLimagolfNameHotelgolfNameDeltamikeNodemikeNodemikeNodemikeNodeFoxtrotoscaralpha
                            )
                            .Name
                        as INamedTypeSymbol
                    );
                }

                object M340() {
                    _ = this.TryFind((SyntaxNode n) => model.GetSymbolInfo(
                            deltagolfNameJulietkilooscargolfNameDeltaindiaSymbolgolfNameechoValueNovemberDeltaechoValuekilokiloPapaFoxtrotosca
                        )
                        is SyntaxNode
                    );
                }

                object M243() {
                    Check(static x => Find(
                            oscaralphaHotelFoxtrotechoValuealphagolfNameechoValuemikeNodegolfNameFoxtrotLimacharliekiloalphaLimachar
                        )
                        is { IsStatic: true }
                    );
                }
            }
            """
        );

    /// <summary>
    ///     Under the broken arrow a property pattern's test stacks as a type's does: the argument two levels past the
    ///     call, the <c>)</c> and <c>is { … }</c> one.
    /// </summary>
    [Fact]
    public void APropertyPattern_StacksAsATypeDoes() =>
        Oracle.Agrees(
            """
            class C {
                async void M908() {
                    await q.ForEach((SyntaxNode n) => Normalize(julietLimamikeNodeHotelechoValueFoxtrotgolfNameLimakiloLimaNovemberindiaSymbolHotelHotelHotelechoValuee)
            is { IsStatic: true });
                }
            }
            """,
            """
            class C {
                async void M908() {
                    await q.ForEach((SyntaxNode n) =>
                        Normalize(
                                julietLimamikeNodeHotelechoValueFoxtrotgolfNameLimakiloLimaNovemberindiaSymbolHotelHotelHotelechoValuee
                            )
                            is { IsStatic: true }
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A later call of two or more followed by a call of none is a later call (the summary of
    ///     <c>GroupFacts.LambdaTestLaterCall</c> said so; the code read <c>.First()</c> as the last call), and behind a
    ///     head of six columns or more it breaks the arrow.
    /// </summary>
    [Fact]
    public void ALaterCallBeforeANoArgumentCall_BreaksTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M250() {
                    result = ExecuteWithRetry(item => Open(bravocharliePapa, EchoValueind).Value.Combine(MikeNodeoscarJulietFoxtro, indiaSymbolFoxtrotBra, foxtrotmikeNodePa).First()
            is SyntaxNode);
                }
                object M290() {
                    var found = list.X(static x => Find(indiaSymbolFoxtrot, papaHotelechoValueos).Value.WithOptions(novemberBravoNovemberechoValu, CharliePapaindiaSymbolalphaalphaDeltakil, charliePapacharlieal).First()
            is SyntaxNode);
                }
            }
            """,
            """
            class C {
                object M250() {
                    result = ExecuteWithRetry(item =>
                        Open(bravocharliePapa, EchoValueind)
                                .Value.Combine(MikeNodeoscarJulietFoxtro, indiaSymbolFoxtrotBra, foxtrotmikeNodePa)
                                .First()
                            is SyntaxNode
                    );
                }

                object M290() {
                    var found = list.X(static x =>
                        Find(indiaSymbolFoxtrot, papaHotelechoValueos)
                                .Value.WithOptions(
                                    novemberBravoNovemberechoValu,
                                    CharliePapaindiaSymbolalphaalphaDeltakil,
                                    charliePapacharlieal
                                )
                                .First()
                            is SyntaxNode
                    );
                }
            }
            """
        );

    /// <summary>
    ///     Behind <c>Run(</c> it is round two's line that breaks it, which the operand reaches only once <c>.First()</c>
    ///     no longer counts as its last call. From probe D (seed 74004).
    /// </summary>
    [Fact]
    public void ALaterCallBeforeANoArgumentCall_ReachesRoundTwosLine() =>
        Oracle.Agrees(
            """
            class C {
                object M2226() {
                    if (c0) { if (c1) { Run(static x => this.cache.Read(hoteloscarBravoBr, charlie).Merge(bravoPapacharliemikeN, indiaSymbolBravoLimaFoxtrot).First()
            is SyntaxNode); } }
                }
            }
            """,
            """
            class C {
                object M2226() {
                    if (c0) {
                        if (c1) {
                            Run(static x =>
                                this.cache.Read(hoteloscarBravoBr, charlie)
                                        .Merge(bravoPapacharliemikeN, indiaSymbolBravoLimaFoxtrot)
                                        .First()
                                    is SyntaxNode
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A later call of two or more behind a head of six columns or more breaks the arrow where round two's line
    ///     kept it: a long parameter list over a wide operand. From probe D (seed 74004), written after the rule.
    /// </summary>
    [Fact]
    public void ALaterCallBehindAHeadOfSix_BreaksTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M170() {
                    Where(item => GetRoot(IndiaSymbolNovember, julietindiaSym).Value.Select(oscarDeltaPapaJulietoscargolfNameJ, deltaLimacharlieBravoNovemberDeltacharlieech, LimaFoxtrotHotelJulietJulietHotelPa, Charlieosca)
            is { IsStatic: true });
                }
                object M650() {
                    Check(static (a, b) => model.GetSymbolInfo(golfNameHotelindiaSy, DeltagolfNameD).Value.Merge(charliekilogolfNameLimaPapagolfNamekiloFox, GolfNameechoValuealphaB, BravoPapaPapagolfNameH)
            is SyntaxNode);
                }
            }
            """,
            """
            class C {
                object M170() {
                    Where(item =>
                        GetRoot(IndiaSymbolNovember, julietindiaSym)
                                .Value.Select(
                                    oscarDeltaPapaJulietoscargolfNameJ,
                                    deltaLimacharlieBravoNovemberDeltacharlieech,
                                    LimaFoxtrotHotelJulietJulietHotelPa,
                                    Charlieosca
                                )
                            is { IsStatic: true }
                    );
                }

                object M650() {
                    Check(static (a, b) =>
                        model.GetSymbolInfo(golfNameHotelindiaSy, DeltagolfNameD)
                                .Value.Merge(
                                    charliekilogolfNameLimaPapagolfNamekiloFox,
                                    GolfNameechoValuealphaB,
                                    BravoPapaPapagolfNameH
                                )
                            is SyntaxNode
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A first call of two or more with links after it breaks the arrow by either of round three's lines: the
    ///     first call with its first link fitting a level in (<c>M1193</c>), or the spread against the arguments' line
    ///     (<c>M425</c>, <c>M1569</c>).
    /// </summary>
    [Fact]
    public void AFirstCallWithLinks_BreaksTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M1193() {
                    return Run((x, y) => GetRoot(Foxtrotcha, DeltaLimaoscari, echoValuePapaNovemberLimaNove, NovemberPapaLimaDeltamikeN).Name.ToList()
            as INamedTypeSymbol);
                }
                object M425() {
                    result = TryFind((SyntaxNode n) => Normalize(DeltaHotel, MikeNodeHotelmikeNodechar, Foxtrotg, novemberLimagolfNameJulietmikeNod).Build().Name.Length
            as INamedTypeSymbol);
                }
                object M1569() {
                    TryFind((SyntaxNode n) => GetRoot(deltakiloHotelHot, PapaoscarFoxtrotindiaSymb, BravoHotelcharlieFoxtrotch).ToList().Name
            is not null);
                }
            }
            """,
            """
            class C {
                object M1193() {
                    return Run((x, y) =>
                        GetRoot(Foxtrotcha, DeltaLimaoscari, echoValuePapaNovemberLimaNove, NovemberPapaLimaDeltamikeN)
                                .Name.ToList()
                            as INamedTypeSymbol
                    );
                }

                object M425() {
                    result = TryFind((SyntaxNode n) =>
                        Normalize(DeltaHotel, MikeNodeHotelmikeNodechar, Foxtrotg, novemberLimagolfNameJulietmikeNod)
                                .Build()
                                .Name.Length
                            as INamedTypeSymbol
                    );
                }

                object M1569() {
                    TryFind((SyntaxNode n) =>
                        GetRoot(deltakiloHotelHot, PapaoscarFoxtrotindiaSymb, BravoHotelcharlieFoxtrotch).ToList().Name
                            is not null
                    );
                }
            }
            """
        );
}
