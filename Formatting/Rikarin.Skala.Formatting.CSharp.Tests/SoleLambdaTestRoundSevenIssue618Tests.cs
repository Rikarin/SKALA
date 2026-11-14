namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #618, SK-DIV-0520 round seven: the call of two weighs the head and the links at two thirds of a column of
///     arguments each and the parameters and the call's head at not quite two fifths, a later call reads its first call
///     only up to 24 columns, and a too-wide argument decides at its own line from column 110. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output, from round six's and this round's probes and grids.
/// </summary>
public sealed class SoleLambdaTestRoundSevenIssue618Tests {
    /// <summary>
    ///     The tie round six's line put three points across (xP M1296), a keep a point the other side and a break on the
    ///     same lattice point behind a narrower head.
    /// </summary>
    [Fact]
    public void ACallOfTwo_TheHeadWeighsTwoThirdsOfAColumn() =>
        Oracle.Agrees(
            """
            class C {
                async void M1296() {
                    if (c0) { await q.ForEach((SyntaxNode n) => model.GetSymbolInfo(LimagolfNameoscarLimacharlieindiaSy, mikeNodeJulietDeltaFoxtrotFoxtro)
            is not null); }
                }
                object M1992() {
                    if (c0) { if (c1) { return Select(static x => model.GetSymbolInfo(bravocharliegolfNamekiloJul, limaNovemberindiaSymbolHotelNovemberP)
            is not null); } }
                }
                object M2368() {
                    if (c0) { _ = this.Run((SyntaxNode n) => model.GetSymbolInfo(indiaSymboloscarmikeNodegolfName, oscargolfNameechoValuemikeNodeLim)
            is SyntaxNode); }
                }
            }
            """,
            """
            class C {
                async void M1296() {
                    if (c0) {
                        await q.ForEach((SyntaxNode n) =>
                            model.GetSymbolInfo(LimagolfNameoscarLimacharlieindiaSy, mikeNodeJulietDeltaFoxtrotFoxtro)
                                is not null
                        );
                    }
                }

                object M1992() {
                    if (c0) {
                        if (c1) {
                            return Select(static x => model.GetSymbolInfo(
                                    bravocharliegolfNamekiloJul,
                                    limaNovemberindiaSymbolHotelNovemberP
                                )
                                is not null
                            );
                        }
                    }
                }

                object M2368() {
                    if (c0) {
                        _ = this.Run((SyntaxNode n) =>
                            model.GetSymbolInfo(indiaSymboloscarmikeNodegolfName, oscargolfNameechoValuemikeNodeLim)
                                is SyntaxNode
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Behind an arrow ending at column 65 or later the line falls a column of arguments: the first breaks, the second
    ///     still keeps.
    /// </summary>
    [Fact]
    public void ACallOfTwoBehindAFarArrow_BreaksAColumnSooner() =>
        Oracle.Agrees(
            """
            class C {
                object M1665() {
                    if (c0) { Zabcdefghijklmnopqrstuvwx(xyyyyyyyyyyyyyyyyyyyyyyyyy => Zabcdefghijklmnopqr(novemberechoValueNovemberkiloPapaPa, mikeNodegolfNameFoxtrotLimamikeNode)
            is not null); }
                }
                object M2007() {
                    if (c0) { Zabcdefghijklmnopqrstuvwxyzab(xyyyyyyyyyyyyyyyyyyy => Zabcdefghijklmnopqr(CharliemikeNodeLimaDeltaalphagolfNamei, kiloalphaechoValueechoValueDeltaNovem)
            is not null); }
                }
            }
            """,
            """
            class C {
                object M1665() {
                    if (c0) {
                        Zabcdefghijklmnopqrstuvwx(xyyyyyyyyyyyyyyyyyyyyyyyyy =>
                            Zabcdefghijklmnopqr(novemberechoValueNovemberkiloPapaPa, mikeNodegolfNameFoxtrotLimamikeNode)
                                is not null
                        );
                    }
                }

                object M2007() {
                    if (c0) {
                        Zabcdefghijklmnopqrstuvwxyzab(xyyyyyyyyyyyyyyyyyyy => Zabcdefghijklmnopqr(
                                CharliemikeNodeLimaDeltaalphagolfNamei,
                                kiloalphaechoValueechoValueDeltaNovem
                            )
                            is not null
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     <c>X(</c>, <c>Do(</c>, <c>Use(</c> in front of a later call: the first call through its <c>)</c> counts in full
    ///     only up to 24 columns, so a long first call no longer keeps the arrow behind a narrow head.
    /// </summary>
    [Fact]
    public void ALaterCallBehindANarrowHead_ReadsTheFirstCallUpTo24Columns() =>
        Oracle.Agrees(
            """
            class C {
                object M2232() {
                    if (c0) { X((string s, int i) => ctx.Resolve().Value.WithOptions(EchoValueHotelDeltaoscarechoValuecharlieD, GolfNameHotelFoxtrotLimaF, G).Name.Length
            is not null); }
                }
                object M32() {
                    X(static (x, y) => ctx.Resolve(limaLi).Value.Merge(PapaLimaechoValuecharlieJulietHotelF, Hot, papaechoValueLimaDeltaalphaB, kilocharlieDeltaF).Name.Length
            is SyntaxNode);
                }
                object M816() {
                    if (c0) { if (c1) { X((SyntaxNode n) => model.GetSymbolInfo(hotelNovemberNovembermikeNodeNovemb).First().Select(indiaSymbolFoxtrotPapaindiaSymbole, oscarkilogolfNa).Name.Length
            is { IsStatic: true }); } }
                }
                object M818() {
                    if (c0) { if (c1) { Use((SyntaxNode n) => model.GetSymbolInfo(hotelNovemberNovembermikeNodeNovemb).First().Select(indiaSymbolFoxtrotPapaindiaSymbole, oscarkilogolfNa).Name.Length
            is { IsStatic: true }); } }
                }
                object M1577() {
                    if (c0) { if (c1) { if (c2) { Z(static node => Compile(LimaJulietNovemberechoValueechoV).First().Zip(In, limaLimaLimaHotelDeltaLimaoscaroscarJ).Name.Length
            is IMethodSymbol); } } }
                }
            }
            """,
            """
            class C {
                object M2232() {
                    if (c0) {
                        X((string s, int i) =>
                            ctx.Resolve()
                                    .Value.WithOptions(EchoValueHotelDeltaoscarechoValuecharlieD, GolfNameHotelFoxtrotLimaF, G)
                                    .Name.Length
                                is not null
                        );
                    }
                }

                object M32() {
                    X(static (x, y) =>
                        ctx.Resolve(limaLi)
                                .Value.Merge(
                                    PapaLimaechoValuecharlieJulietHotelF,
                                    Hot,
                                    papaechoValueLimaDeltaalphaB,
                                    kilocharlieDeltaF
                                )
                                .Name.Length
                            is SyntaxNode
                    );
                }

                object M816() {
                    if (c0) {
                        if (c1) {
                            X((SyntaxNode n) => model.GetSymbolInfo(hotelNovemberNovembermikeNodeNovemb)
                                    .First()
                                    .Select(indiaSymbolFoxtrotPapaindiaSymbole, oscarkilogolfNa)
                                    .Name.Length
                                is { IsStatic: true }
                            );
                        }
                    }
                }

                object M818() {
                    if (c0) {
                        if (c1) {
                            Use((SyntaxNode n) =>
                                model.GetSymbolInfo(hotelNovemberNovembermikeNodeNovemb)
                                        .First()
                                        .Select(indiaSymbolFoxtrotPapaindiaSymbole, oscarkilogolfNa)
                                        .Name.Length
                                    is { IsStatic: true }
                            );
                        }
                    }
                }

                object M1577() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                Z(static node => Compile(LimaJulietNovemberechoValueechoV)
                                        .First()
                                        .Zip(In, limaLimaLimaHotelDeltaLimaoscaroscarJ)
                                        .Name.Length
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
    ///     An argument line ending at 111 to 113 a point or two under the line keeps the arrow, and one on the line breaks
    ///     it whether or not the operand fits a level in.
    /// </summary>
    [Fact]
    public void ATooWideArgumentFromColumn110_DecidesAtItsOwnLine() =>
        Oracle.Agrees(
            """
            class C {
                object M423() {
                    Validate(static (x, y) => Open(CharliePapaJulietJulietoscaroscarJulietoscarechoValueechoValuealphacharlieindiaSymbolalphaHotelL)
            is not null);
                }
                object M2196() {
                    items.TryFind((x, y) => context.Workspace.Get(alphaPapaechoValuecharliealphaNovemberechoValueLimaBravooscarindiaSymbolNovemberDeltaJulietecho)
            is SyntaxNode);
                }
                object M1524() {
                    _ = this.Do(static (x, y) => Normalize(CharlieoscarLimaindiaSymbolBravoindiaSymbolFoxtrotBravoHotelFoxtrotgolfNameNovemberkiloalphaDelt)
            is SyntaxNode);
                }
                object M1434() {
                    return Bind((T t) => root.Descendants(HotelDeltaoscarechoValueechoValueDeltacharlieindiaSymbolcharlieDeltaindiaSymbolPapagolfNameFoxtr)
            is { Length: > 0 });
                }
                object M430() {
                    q.ForEach(static (x, y) => Open(CharliePapaJulietJulietoscaroscarJulietoscarechoValueechoValuealphacharlieindiaSymbolalphaHotelL)
            is not null);
                }
            }
            """,
            """
            class C {
                object M423() {
                    Validate(static (x, y) => Open(
                            CharliePapaJulietJulietoscaroscarJulietoscarechoValueechoValuealphacharlieindiaSymbolalphaHotelL
                        )
                        is not null
                    );
                }

                object M2196() {
                    items.TryFind((x, y) => context.Workspace.Get(
                            alphaPapaechoValuecharliealphaNovemberechoValueLimaBravooscarindiaSymbolNovemberDeltaJulietecho
                        )
                        is SyntaxNode
                    );
                }

                object M1524() {
                    _ = this.Do(static (x, y) => Normalize(
                            CharlieoscarLimaindiaSymbolBravoindiaSymbolFoxtrotBravoHotelFoxtrotgolfNameNovemberkiloalphaDelt
                        )
                        is SyntaxNode
                    );
                }

                object M1434() {
                    return Bind((T t) =>
                        root.Descendants(
                                HotelDeltaoscarechoValueechoValueDeltacharlieindiaSymbolcharlieDeltaindiaSymbolPapagolfNameFoxtr
                            )
                            is { Length: > 0 }
                    );
                }

                object M430() {
                    q.ForEach(static (x, y) =>
                        Open(CharliePapaJulietJulietoscaroscarJulietoscarechoValueechoValuealphacharlieindiaSymbolalphaHotelL)
                            is not null
                    );
                }
            }
            """
        );
}
