namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #618, SK-DIV-0520 round five: what the kept arrow's first line would have to hold decides first. Every
///     expected string is <c>jb cleanupcode</c> 2025.2.6's own output, from round four's probes and grids.
/// </summary>
public sealed class SoleLambdaTestRoundFiveIssue618Tests {
    /// <summary>
    ///     Where the arrow's line holds the first call, the arrow stays while it ends less than twelve columns past the
    ///     line's start, and parameters of five columns or fewer break it from there.
    /// </summary>
    [Fact]
    public void ASimpleParameterList_BreaksTheArrowFromTwelveColumns() =>
        Oracle.Agrees(
            """
            class C {
                object M465() {
                    if (c0) { return X(item => Analyze(alphaHote, HotelNovemberDel, limagolfNamecharliegolfNameHote, HotelNovemberkilom).Name.Length
            as INamedTypeSymbol); }
                }
                object M2249() {
                    Where(item => Lookup(alphaalphamikeNodeLimaBrav, HotelPapami, KiloindiaSymboloscarJ, PapagolfNamekiloalpha).Build().Result
            is SyntaxNode);
                }
                object M231() {
                    if (c0) { if (c1) { if (c2) { Do(x => x.DeltakiloPapakilogolfNamecharlieg.GolfNamegolfNamecha.LimaHotelNovemberFoxtrotindiaSymbolFo.PapaHotelechoValuealphaBravoF.LimaFoxtrotLimacharliekilo
            is SyntaxNode); } } }
                }
                object M394() {
                    if (c0) { if (c1) { Do(item => Find(IndiaSymbolDeltakilomikeNodeindiaSymbol).Analyze(KiloJulietindiaSymbolHo, alphaHotelPapaDeltaoscarDelt, Ch, kiloindiaSymbo)
            as INamedTypeSymbol); } }
                }
            }
            """,
            """
            class C {
                object M465() {
                    if (c0) {
                        return X(item =>
                            Analyze(alphaHote, HotelNovemberDel, limagolfNamecharliegolfNameHote, HotelNovemberkilom).Name.Length
                                as INamedTypeSymbol
                        );
                    }
                }

                object M2249() {
                    Where(item =>
                        Lookup(alphaalphamikeNodeLimaBrav, HotelPapami, KiloindiaSymboloscarJ, PapagolfNamekiloalpha).Build().Result
                            is SyntaxNode
                    );
                }

                object M231() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                Do(x => x.DeltakiloPapakilogolfNamecharlieg.GolfNamegolfNamecha
                                        .LimaHotelNovemberFoxtrotindiaSymbolFo.PapaHotelechoValuealphaBravoF
                                        .LimaFoxtrotLimacharliekilo
                                    is SyntaxNode
                                );
                            }
                        }
                    }
                }

                object M394() {
                    if (c0) {
                        if (c1) {
                            Do(item => Find(IndiaSymbolDeltakilomikeNodeindiaSymbol)
                                    .Analyze(KiloJulietindiaSymbolHo, alphaHotelPapaDeltaoscarDelt, Ch, kiloindiaSymbo)
                                as INamedTypeSymbol
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Typed or <c>static</c> parameters break the arrow wherever the operand fits a level in with eleven columns to
    ///     spare.
    /// </summary>
    [Fact]
    public void AnOperandWithRoomOnTheLineBelow_BreaksTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M1065() {
                    if (c0) { if (c1) { if (c2) { Check(static (x, y) => Resolve(PapaJulietJulietJuli, oscarmikeNodeoscarkilomikeNodePapaL, alphaHote).Result
            as INamedTypeSymbol); } } }
                }
                object M369() {
                    if (c0) { Do((Node node) => Analyze(NovemberDeltaFoxtrotmik, novemberLimaFoxtrot, OscarLimaFoxtrotmikeNodekiloBra).Name
            as INamedTypeSymbol); }
                }
                object M624() {
                    X((SyntaxNode n) => Find(indiaSymbolPa).Combine(charlieNovembercharlieFoxtrotkilo, indiaSymbolalphaFoxtro).Result
            is { IsStatic: true });
                }
            }
            """,
            """
            class C {
                object M1065() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                Check(static (x, y) =>
                                    Resolve(PapaJulietJulietJuli, oscarmikeNodeoscarkilomikeNodePapaL, alphaHote).Result
                                        as INamedTypeSymbol
                                );
                            }
                        }
                    }
                }

                object M369() {
                    if (c0) {
                        Do((Node node) =>
                            Analyze(NovemberDeltaFoxtrotmik, novemberLimaFoxtrot, OscarLimaFoxtrotmikeNodekiloBra).Name
                                as INamedTypeSymbol
                        );
                    }
                }

                object M624() {
                    X((SyntaxNode n) =>
                        Find(indiaSymbolPa).Combine(charlieNovembercharlieFoxtrotkilo, indiaSymbolalphaFoxtro).Result
                            is { IsStatic: true }
                    );
                }
            }
            """
        );

    /// <summary>
    ///     Past those, a line per operand class: a later call, a first call with links, a property chain, a property run
    ///     ending in a call of no arguments, and a one-argument call that fits beside the arrow.
    /// </summary>
    [Fact]
    public void PastTheRoom_ALinePerOperandClass_Decides() =>
        Oracle.Agrees(
            """
            class C {
                object M574() {
                    if (c0) { if (c1) { Use((Node node) => context.Workspace.Get(mikeNodeechoVa, PapaNovemberJul).Analyze(IndiaSymbolBravoPapaoscarindiaSym, EchoValue).Result
            is SyntaxNode); } }
                }
                object M705() {
                    if (c0) { Where((Node node) => Resolve(kilooscaralphaoscarJulietmikeNodeJ, CharlieL, KiloJulietkilochar, mikeNodeLi).Name.Result
            as INamedTypeSymbol); }
                }
                object M1815() {
                    X(static (a, b) => julietFox.LimaoscarDelta.IndiaSymb.FoxtrotDeltaHotelPapaosca.HotelDeltaP.NovemberkiloalphagolfNameosca
            as INamedTypeSymbol);
                }
                object M110() {
                    X(static (x, y) => novemberLimaDeltaBravokil.AlphaLimaoscaralphacharlieLimaLim.PapaechoValueHoteloscarBravoNovemb.BravogolfNamecharlieki.GetName()
            is SyntaxNode);
                }
                object M1400() {
                    X(static x => Lookup(charlieoscargolfNamePapaalphaNovemberPapacharliegolfNameDeltaJulietJulietFoxtrotF).Result.Name
            is { IsStatic: true });
                }
            }
            """,
            """
            class C {
                object M574() {
                    if (c0) {
                        if (c1) {
                            Use((Node node) =>
                                context.Workspace.Get(mikeNodeechoVa, PapaNovemberJul)
                                        .Analyze(IndiaSymbolBravoPapaoscarindiaSym, EchoValue)
                                        .Result
                                    is SyntaxNode
                            );
                        }
                    }
                }

                object M705() {
                    if (c0) {
                        Where((Node node) =>
                            Resolve(kilooscaralphaoscarJulietmikeNodeJ, CharlieL, KiloJulietkilochar, mikeNodeLi).Name.Result
                                as INamedTypeSymbol
                        );
                    }
                }

                object M1815() {
                    X(static (a, b) => julietFox.LimaoscarDelta.IndiaSymb.FoxtrotDeltaHotelPapaosca.HotelDeltaP
                            .NovemberkiloalphagolfNameosca
                        as INamedTypeSymbol
                    );
                }

                object M110() {
                    X(static (x, y) => novemberLimaDeltaBravokil.AlphaLimaoscaralphacharlieLimaLim
                            .PapaechoValueHoteloscarBravoNovemb.BravogolfNamecharlieki.GetName()
                        is SyntaxNode
                    );
                }

                object M1400() {
                    X(static x => Lookup(charlieoscargolfNamePapaalphaNovemberPapacharliegolfNameDeltaJulietJulietFoxtrotF)
                            .Result.Name
                        is { IsStatic: true }
                    );
                }
            }
            """
        );

    /// <summary>
    ///     Where the arrow's line cannot hold a one-argument call, <c>5·(c + t) − 3·(p + h) ≥ min(9, 7·e − 790)</c>
    ///     decides, a point lower for parameters of ten columns or more.
    /// </summary>
    [Fact]
    public void AOneArgumentCallTooWideBeside_TheArgumentsLineDecides() =>
        Oracle.Agrees(
            """
            class C {
                object M612() {
                    if (c0) { TryFind((x, y) => Find(julietcharliegolfNameLimaoscarJulietechoValueBravooscarkilomikeNodeoscarNovemberLimacharlieLimakil)
            as INamedTypeSymbol); }
                }
                object M956() {
                    _ = this.Do(static (a, b) => ctx.Resolve(limamikeNodeFoxtrotDeltaechoValuePapakilogolfNameDeltacharlieBravoDeltaBravokiloindiaSymbolBravoNov).Name
            is not null);
                }
                object M1460() {
                    if (c0) { if (c1) { if (c2) { total += Inspect(static (m) => factory.Create(IndiaSymbolDeltaLimaLimaBravomikeNodealphamikeNodeDeltacharlieindiaSymbolgolfNameHotelBravoindiaSymbolc)
            is IMethodSymbol); } } }
                }
            }
            """,
            """
            class C {
                object M612() {
                    if (c0) {
                        TryFind((x, y) => Find(
                                julietcharliegolfNameLimaoscarJulietechoValueBravooscarkilomikeNodeoscarNovemberLimacharlieLimakil
                            )
                            as INamedTypeSymbol
                        );
                    }
                }

                object M956() {
                    _ = this.Do(static (a, b) =>
                        ctx.Resolve(
                                    limamikeNodeFoxtrotDeltaechoValuePapakilogolfNameDeltacharlieBravoDeltaBravokiloindiaSymbolBravoNov
                                )
                                .Name
                            is not null
                    );
                }

                object M1460() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                total += Inspect(static (m) =>
                                    factory.Create(
                                            IndiaSymbolDeltaLimaLimaBravomikeNodealphamikeNodeDeltacharlieindiaSymbolgolfNameHotelBravoindiaSymbolc
                                        )
                                        is IMethodSymbol
                                );
                            }
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A chain's trailing property run that ends in a call of no arguments breaks before that call when it runs past
    ///     the margin.
    /// </summary>
    [Fact]
    public void ATrailingPropertyRunPastTheMargin_BreaksBeforeItsCall() =>
        Oracle.Agrees(
            """
            class C {
                object M1349() {
                    if (c0) { if (c1) { if (c2) { _ = Transform(y => root.Descendants().IndiaSymbolechoValuealphaHote.HotelcharlieLimakilogolfNa.KiloNovembermikeNodeNovember.ToList()
            as string); } } }
                }
                object M653() {
                    if (c0) { this.list.Each(y => symbol.Find().FoxtrotNovemberechoValueoscara.CharlieFoxtrotkiloechoValue.GolfNamecharlieindiaSymbolJuli.Build()
            is not { }); }
                }
            }
            """,
            """
            class C {
                object M1349() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                _ = Transform(y =>
                                    root.Descendants()
                                            .IndiaSymbolechoValuealphaHote.HotelcharlieLimakilogolfNa.KiloNovembermikeNodeNovember
                                            .ToList()
                                        as string
                                );
                            }
                        }
                    }
                }

                object M653() {
                    if (c0) {
                        this.list.Each(y =>
                            symbol.Find()
                                    .FoxtrotNovemberechoValueoscara.CharlieFoxtrotkiloechoValue.GolfNamecharlieindiaSymbolJuli
                                    .Build()
                                is not { }
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A property run past the margin by its last <c>()</c> alone breaks the arrow even behind a short head.
    /// </summary>
    [Fact]
    public void APropertyRunPastTheMarginByItsParentheses_BreaksTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M38() {
                    if (c0) { Use(item => echoValueBravocharliemikeN.EchoValuekiloJulietDeltaJulietosc.JulietFoxtrotcharlieoscarech.ToList()
            is SyntaxNode); }
                }
                object M2174() {
                    if (c0) { X((x) => julietHotelcharlieLimaechoVal.OscaralphamikeNodeJulietalph.LimaoscarNovembergolfN.Kilocharli.Build()
            as INamedTypeSymbol); }
                }
            }
            """,
            """
            class C {
                object M38() {
                    if (c0) {
                        Use(item =>
                            echoValueBravocharliemikeN.EchoValuekiloJulietDeltaJulietosc.JulietFoxtrotcharlieoscarech.ToList()
                                is SyntaxNode
                        );
                    }
                }

                object M2174() {
                    if (c0) {
                        X((x) =>
                            julietHotelcharlieLimaechoVal.OscaralphamikeNodeJulietalph.LimaoscarNovembergolfN.Kilocharli.Build()
                                as INamedTypeSymbol
                        );
                    }
                }
            }
            """
        );
}
