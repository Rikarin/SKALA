namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #618, SK-DIV-0520 round four: behind a narrow head — <c>X(</c>, <c>Do(</c>, <c>Visit(</c> — the oracle
///     weighs the head through the outer call's <c>(</c> against the parameters and the line's start. Every expected
///     string is <c>jb cleanupcode</c> 2025.2.6's own output: from probe K (seed 78001) and the head-width grid E (seed
///     78002), both written after the rules, unless a test names another probe.
/// </summary>
public sealed class SoleLambdaTestRoundFourIssue618Tests {
    /// <summary>A property chain keeps the arrow and fills beside it behind a head the parameters outweigh.</summary>
    [Fact]
    public void APropertyChainBehindANarrowHead_KeepsTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M815() {
                    X((SyntaxNode n) => this.model.LimaPapaBravoHotelPapaech.Foxtr.OscarDeltaBravoPapaosc.PapaDeltaDeltaoscarLimachar.LimaBravoLimaNovemberkilo
            is not null);
                }
                object M2143() {
                    Do((x, y) => this.model.MikeN.EchoValueechoValuegolfNa.AlphacharlieechoValueBravoHotel.November.OscaroscarcharlieindiaSymbolHotelNovem
            is { IsStatic: true });
                }
                object M384() {
                    if (c0) { if (c1) { X((a, b, c) => limacharliePapaecho.GolfNamegolf.JulietLimaoscarLimagolfNamemikeNodeal.AlphaLimakiloHotelLimamikeNode.LimaDeltaPapamikeNodeDeltaL.LimagolfNamePapaFoxtrotech
            is not { }); } }
                }
            }
            """,
            """
            class C {
                object M815() {
                    X((SyntaxNode n) => this.model.LimaPapaBravoHotelPapaech.Foxtr.OscarDeltaBravoPapaosc
                            .PapaDeltaDeltaoscarLimachar.LimaBravoLimaNovemberkilo
                        is not null
                    );
                }

                object M2143() {
                    Do((x, y) => this.model.MikeN.EchoValueechoValuegolfNa.AlphacharlieechoValueBravoHotel.November
                            .OscaroscarcharlieindiaSymbolHotelNovem
                        is { IsStatic: true }
                    );
                }

                object M384() {
                    if (c0) {
                        if (c1) {
                            X((a, b, c) => limacharliePapaecho.GolfNamegolf.JulietLimaoscarLimagolfNamemikeNodeal
                                    .AlphaLimakiloHotelLimamikeNode.LimaDeltaPapamikeNodeDeltaL.LimagolfNamePapaFoxtrotech
                                is not { }
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A one-argument call whose argument's line falls short of column 114 still keeps the arrow far enough
    ///     inside the line, and one that fits on the line below keeps it as one that does not.
    /// </summary>
    [Fact]
    public void AOneArgumentCallShortOfColumn114_KeepsTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M499() {
                    if (c0) { Select((Node node) => ctx.Resolve(limaechoValuePapaJulietLimaNovemberechoValueJulietPapacharlieNovemberJulietoscarPapacharl)
            is { IsStatic: true }); }
                }
                object M1867() {
                    Visit((x, y) => ctx.Resolve(MikeNodeoscarcharliekiloFoxtrotmikeNodecharliealphagolfNameechoValueJulietmikeNodeHotelPapamikeN)
            as INamedTypeSymbol);
                }
                object M1186() {
                    Use((T t) => Make(GolfNameDeltaalphaDeltaindiaSymboloscarHotelFoxtrotalphacharlieoscaroscarNovembermikeNodemikeNode)
            as string);
                }
            }
            """,
            """
            class C {
                object M499() {
                    if (c0) {
                        Select((Node node) => ctx.Resolve(
                                limaechoValuePapaJulietLimaNovemberechoValueJulietPapacharlieNovemberJulietoscarPapacharl
                            )
                            is { IsStatic: true }
                        );
                    }
                }

                object M1867() {
                    Visit((x, y) => ctx.Resolve(
                            MikeNodeoscarcharliekiloFoxtrotmikeNodecharliealphagolfNameechoValueJulietmikeNodeHotelPapamikeN
                        )
                        as INamedTypeSymbol
                    );
                }

                object M1186() {
                    Use((T t) => Make(
                            GolfNameDeltaalphaDeltaindiaSymboloscarHotelFoxtrotalphacharlieoscaroscarNovembermikeNodemikeNode
                        )
                        as string
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A later call keeps the arrow over a wide first call behind a narrow head, the operand far past the margin.
    ///     From probe D (seed 74004) and grid A (seed 75001).
    /// </summary>
    [Fact]
    public void ALaterCallOverAWideFirstCall_KeepsTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M354() {
                    X(static x => model.GetSymbolInfo(MikeNodeFoxtro).First().Select(H, FoxtrotHotelFoxtrotcharlie, IndiaSymbolalphaHotelalphaalphaHotelDe).First()
            as INamedTypeSymbol);
                }
                object M144() {
                    if (c0) { X((SyntaxNode n) => Resolve(LimaoscarechoValueLimaindiaSymbolgolfNam).Combine(MikeNodeBra, novemberDeltamikeNodeJulietN).First()
            is { IsStatic: true }); }
                }
            }
            """,
            """
            class C {
                object M354() {
                    X(static x => model.GetSymbolInfo(MikeNodeFoxtro)
                            .First()
                            .Select(H, FoxtrotHotelFoxtrotcharlie, IndiaSymbolalphaHotelalphaalphaHotelDe)
                            .First()
                        as INamedTypeSymbol
                    );
                }

                object M144() {
                    if (c0) {
                        X((SyntaxNode n) => Resolve(LimaoscarechoValueLimaindiaSymbolgolfNam)
                                .Combine(MikeNodeBra, novemberDeltamikeNodeJulietN)
                                .First()
                            is { IsStatic: true }
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A first call with links breaks the arrow where it would end early enough a level in, behind a narrow head
    ///     included. From probe D (seed 74004) and grid B (seed 75002).
    /// </summary>
    [Fact]
    public void AFirstCallWithLinksEndingEarlyBelow_BreaksTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M617() {
                    if (c0) { Run(static (x, y) => Normalize(novemberHotelBravoNovemberJulie, LimakiloDeltaHotelJulietFox).Name.Length.First()
            is { IsStatic: true }); }
                }
                object M1376() {
                    if (c0) { if (c1) { if (c2) { X((SyntaxNode n) => Resolve(limaHotelkiloDel, delta, novemberFoxtrotHotelNo, MikeNodeL).ToList().Result
            is { IsStatic: true }); } } }
                }
            }
            """,
            """
            class C {
                object M617() {
                    if (c0) {
                        Run(static (x, y) =>
                            Normalize(novemberHotelBravoNovemberJulie, LimakiloDeltaHotelJulietFox).Name.Length.First()
                                is { IsStatic: true }
                        );
                    }
                }

                object M1376() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                X((SyntaxNode n) =>
                                    Resolve(limaHotelkiloDel, delta, novemberFoxtrotHotelNo, MikeNodeL).ToList().Result
                                        is { IsStatic: true }
                                );
                            }
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A call of two or more with nothing after it, by round four's line where it is five points or more from the
    ///     boundary: broken over <c>M575</c> (probe C, seed 74003), kept over <c>M2264</c> (probe K) and <c>M376</c>
    ///     (probe G, seed 74901).
    /// </summary>
    [Fact]
    public void ACallOfTwoOrMore_FollowsTheHeadLine() =>
        Oracle.Agrees(
            """
            class C {
                object M575() {
                    return Use(static x => this.cache.Read(foxtrotNovemberHote, MikeNodegolfNamealphaP, oscarLima, AlphagolfNameosca)
            is SyntaxNode);
                }
                async void M2264() {
                    if (c0) { if (c1) { if (c2) { await q.Apply((x, y) => this.cache.Read(AlphaFoxtr, echoValueBravomi, golfNameHotelJu, deltaHotelalphaNove)
            is { IsStatic: true }); } } }
                }
                object M376() {
                    if (c0) { if (c1) { var r = s.Filter((string s, int i) => state.Store.Lookup(hotelPapa, limaDeltaBravokilooscarmikeNodegolfName, LimaindiaSymbo)
            is not { }); } }
                }
            }
            """,
            """
            class C {
                object M575() {
                    return Use(static x =>
                        this.cache.Read(foxtrotNovemberHote, MikeNodegolfNamealphaP, oscarLima, AlphagolfNameosca)
                            is SyntaxNode
                    );
                }

                async void M2264() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                await q.Apply((x, y) => this.cache.Read(
                                        AlphaFoxtr,
                                        echoValueBravomi,
                                        golfNameHotelJu,
                                        deltaHotelalphaNove
                                    )
                                    is { IsStatic: true }
                                );
                            }
                        }
                    }
                }

                object M376() {
                    if (c0) {
                        if (c1) {
                            var r = s.Filter((string s, int i) => state.Store.Lookup(
                                    hotelPapa,
                                    limaDeltaBravokilooscarmikeNodegolfName,
                                    LimaindiaSymbo
                                )
                                is not { }
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>A later call breaks the arrow behind a narrow head that outweighs the parameters and the first call.</summary>
    [Fact]
    public void ALaterCallBehindANarrowHead_BreaksTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M2330() {
                    if (c0) { if (c1) { if (c2) { X(static x => context.Workspace.Get().WithOptions(indiaSymbolkiloDeltaDeltaFoxtrot, LimaindiaSymbolalphaBravoechoValueLimaDelta, J).Name.Length
            as INamedTypeSymbol); } } }
                }
                object M49() {
                    if (c0) { if (c1) { Do((a, b, c) => root.Descendants(golfNameDel, Char).Apply(FoxtrotHotelec, OscarkiloJulietPapaalp, indiaSymbolmikeNodeki, AlphaindiaSymbol)
            as string); } }
                }
            }
            """,
            """
            class C {
                object M2330() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                X(static x =>
                                    context.Workspace.Get()
                                            .WithOptions(
                                                indiaSymbolkiloDeltaDeltaFoxtrot,
                                                LimaindiaSymbolalphaBravoechoValueLimaDelta,
                                                J
                                            )
                                            .Name.Length
                                        as INamedTypeSymbol
                                );
                            }
                        }
                    }
                }

                object M49() {
                    if (c0) {
                        if (c1) {
                            Do((a, b, c) =>
                                root.Descendants(golfNameDel, Char)
                                        .Apply(FoxtrotHotelec, OscarkiloJulietPapaalp, indiaSymbolmikeNodeki, AlphaindiaSymbol)
                                    as string
                            );
                        }
                    }
                }
            }
            """
        );
}
