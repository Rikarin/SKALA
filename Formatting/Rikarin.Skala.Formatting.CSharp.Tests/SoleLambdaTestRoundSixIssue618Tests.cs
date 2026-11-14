namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #618, SK-DIV-0520 round six: a call of two or more that does not fit beside the arrow is weighed by where
///     its arguments end, and a chain held beside the arrow by where the kept arrow's first line ends. Every expected
///     string is <c>jb cleanupcode</c> 2025.2.6's own output, from round four's and five's probes and this round's grids.
/// </summary>
public sealed class SoleLambdaTestRoundSixIssue618Tests {
    /// <summary>
    ///     A call of two or more arguments that the arrow's line cannot hold, links after it or none: the spread
    ///     <c>5·(c + r) − 3·(p + h)</c> against where the arguments end, <c>i + a</c>.
    /// </summary>
    [Fact]
    public void ACallOfTwoTooWideBeside_TheArgumentsEndDecides() =>
        Oracle.Agrees(
            """
            class C {
                object M528() {
                    return Analyze((x, y) => GetRoot(HotelLim, kilogol, echoValuemikeNodeoscarFoxtrotechoV, deltaBravoalphaPapamikeNo)
            is not null);
                }
                async void M1304() {
                    if (c0) { await q.RegisterAction(_ => Normalize(julietLimaFoxtrotoscarNovemberalphain, OscarHotelNovember, PapaindiaSymbol, GolfNam)
            is not null); }
                }
                object M993() {
                    if (c0) { if (c1) { bool ok = nodes.Run((x, y) => context.Workspace.Get(kilogolfNameJulie, julietcharlieJulietDeltaFoxtr, PapaHotelFo, julietDeltaoscarmi).ToList().First()
            is not null); } }
                }
                object M337() {
                    _ = this.Where((x) => ctx.Resolve(NovemberLimamikeNod, mikeNodeNovemberalphagolfNamego, mikeNodeBravoFoxtrotalphagolfNa).Name
            is not null);
                }
            }
            """,
            """
            class C {
                object M528() {
                    return Analyze((x, y) =>
                        GetRoot(HotelLim, kilogol, echoValuemikeNodeoscarFoxtrotechoV, deltaBravoalphaPapamikeNo)
                            is not null
                    );
                }

                async void M1304() {
                    if (c0) {
                        await q.RegisterAction(_ => Normalize(
                                julietLimaFoxtrotoscarNovemberalphain,
                                OscarHotelNovember,
                                PapaindiaSymbol,
                                GolfNam
                            )
                            is not null
                        );
                    }
                }

                object M993() {
                    if (c0) {
                        if (c1) {
                            bool ok = nodes.Run((x, y) =>
                                context.Workspace.Get(
                                            kilogolfNameJulie,
                                            julietcharlieJulietDeltaFoxtr,
                                            PapaHotelFo,
                                            julietDeltaoscarmi
                                        )
                                        .ToList()
                                        .First()
                                    is not null
                            );
                        }
                    }
                }

                object M337() {
                    _ = this.Where((x) => ctx.Resolve(
                                NovemberLimamikeNod,
                                mikeNodeNovemberalphagolfNamego,
                                mikeNodeBravoFoxtrotalphagolfNa
                            )
                            .Name
                        is not null
                    );
                }
            }
            """
        );

    /// <summary>
    ///     Where the call fits neither beside the arrow nor a level in, the spread alone decides, capped at 165.
    /// </summary>
    [Fact]
    public void LongLinksBehindAWideHead_BreakTheArrowWhereTheCallFitsNowhere() =>
        Oracle.Agrees(
            """
            class C {
                object M345() {
                    if (c0) { bool ok = nodes.ExecuteWithRetry((Node node) => model.GetSymbolInfo(BravoJulietBravoHo, IndiaSymbolJulietFoxtrotHot, novemberechoValueBravocharliemik, hotelNovemberPapaFoxtrotPapam).Name.Length.ToList()
            is SyntaxNode); }
                }
                object M1575() {
                    var found = list.RegisterAction(x => Analyze(BravoLimaFoxtrotalphaFoxtrotoscarH, alphakiloHotel, KiloindiaSymbolalphaindiaSymbolos, indiaSymbolFoxtrotkilo).Build()
            as INamedTypeSymbol);
                }
            }
            """,
            """
            class C {
                object M345() {
                    if (c0) {
                        bool ok = nodes.ExecuteWithRetry((Node node) =>
                            model.GetSymbolInfo(
                                        BravoJulietBravoHo,
                                        IndiaSymbolJulietFoxtrotHot,
                                        novemberechoValueBravocharliemik,
                                        hotelNovemberPapaFoxtrotPapam
                                    )
                                    .Name.Length.ToList()
                                is SyntaxNode
                        );
                    }
                }

                object M1575() {
                    var found = list.RegisterAction(x =>
                        Analyze(
                                    BravoLimaFoxtrotalphaFoxtrotoscarH,
                                    alphakiloHotel,
                                    KiloindiaSymbolalphaindiaSymbolos,
                                    indiaSymbolFoxtrotkilo
                                )
                                .Build()
                            as INamedTypeSymbol
                    );
                }
            }
            """
        );

    /// <summary>
    ///     Behind one head, one parameter list and one indent, chains of growing width keep the arrow where the kept fill's
    ///     first line comes near the margin and break it where that line would leave a gap.
    /// </summary>
    [Fact]
    public void APropertyChain_TheKeptFillsFirstLineDecides() =>
        Oracle.Agrees(
            """
            class C {
                object M544() {
                    Find((Node node) => alpha.CharlieJulietCharlieEcho.EchoCh.BravoIndiaE.DeltaHote.GolfGolfJulietCharli.LimaHotelLima.India
            as string);
                }
                object M545() {
                    Find((Node node) => ctx.AlphaKiloFoxtrotFoxtrotDeltaGo.CharlieBravoKiloEchoCha.JulietHotelJulietJulietIn.IndiaEchoFoxtrot
            is SyntaxNode);
                }
                object M547() {
                    Find((Node node) => alphaI.HotelGolfB.EchoGolfEc.CharlieLimaBravoFoxtrot.CharlieEchoIndiaHotelFo.BravoDeltaBravoFoxtrotBravoGo.Charli
            as string);
                }
                object M548() {
                    Find((Node node) => x.KiloIn.CharlieFoxtrotBravoFoxtrot.EchoBravoKiloDeltaCharl.DeltaKiloK.KiloHotelLimaEchoI.Foxtrot.FoxtrotGolfJulietDelta
            as string);
                }
            }
            """,
            """
            class C {
                object M544() {
                    Find((Node node) =>
                        alpha.CharlieJulietCharlieEcho.EchoCh.BravoIndiaE.DeltaHote.GolfGolfJulietCharli.LimaHotelLima.India
                            as string
                    );
                }

                object M545() {
                    Find((Node node) => ctx.AlphaKiloFoxtrotFoxtrotDeltaGo.CharlieBravoKiloEchoCha.JulietHotelJulietJulietIn
                            .IndiaEchoFoxtrot
                        is SyntaxNode
                    );
                }

                object M547() {
                    Find((Node node) =>
                        alphaI.HotelGolfB.EchoGolfEc.CharlieLimaBravoFoxtrot.CharlieEchoIndiaHotelFo.BravoDeltaBravoFoxtrotBravoGo
                                .Charli
                            as string
                    );
                }

                object M548() {
                    Find((Node node) => x.KiloIn.CharlieFoxtrotBravoFoxtrot.EchoBravoKiloDeltaCharl.DeltaKiloK.KiloHotelLimaEchoI
                            .Foxtrot.FoxtrotGolfJulietDelta
                        as string
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A property run ending in a call of no arguments, by the same column at a lower threshold.
    /// </summary>
    [Fact]
    public void APropertyRunEndingInACall_TheKeptFillsFirstLineDecides() =>
        Oracle.Agrees(
            """
            class C {
                object M38() {
                    if (c0) { Use(item => echoValueBravocharliemikeN.EchoValuekiloJulietDeltaJulietosc.JulietFoxtrotcharlieoscarech.ToList()
            is SyntaxNode); }
                }
                object M142() {
                    if (c0) { X(static (x, y) => oscarkilooscarmikeNodeecho.AlphaalphaDeltaNovemberalphamike.EchoValuePapaL.OscarDeltaindiaSymbolechoV.First()
            is SyntaxNode); }
                }
                object M558() {
                    if (c0) { X(static (a, b) => charlieHot.LimamikeNodeoscarindiaSymbol.LimaechoValuealphaechoValueBra.FoxtrotmikeNodeosca.Build()
            is SyntaxNode); }
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

                object M142() {
                    if (c0) {
                        X(static (x, y) => oscarkilooscarmikeNodeecho.AlphaalphaDeltaNovemberalphamike.EchoValuePapaL
                                .OscarDeltaindiaSymbolechoV.First()
                            is SyntaxNode
                        );
                    }
                }

                object M558() {
                    if (c0) {
                        X(static (a, b) => charlieHot.LimamikeNodeoscarindiaSymbol.LimaechoValuealphaechoValueBra
                                .FoxtrotmikeNodeosca.Build()
                            is SyntaxNode
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A first call of two or more held beside the arrow, links after it: where its <c>)</c> ends the kept line.
    /// </summary>
    [Fact]
    public void AHeldFirstCallWithLinks_ItsClosingParenthesisDecides() =>
        Oracle.Agrees(
            """
            class C {
                object M465() {
                    if (c0) { return X(item => Analyze(alphaHote, HotelNovemberDel, limagolfNamecharliegolfNameHote, HotelNovemberkilom).Name.Length
            as INamedTypeSymbol); }
                }
                object M401() {
                    if (c0) { if (c1) { Do((SyntaxNode n) => Find(OscarDelt, OscarechoV, papaDeltaBravom, NovemberDeltaoscarPapamikeNo).Name.Length.Result
            is not null); } }
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

                object M401() {
                    if (c0) {
                        if (c1) {
                            Do((SyntaxNode n) => Find(OscarDelt, OscarechoV, papaDeltaBravom, NovemberDeltaoscarPapamikeNo)
                                    .Name.Length.Result
                                is not null
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A held call's lambda over <c>x is A or B</c>, its line's own column short of the floor: the arrow stays
    ///     where the body moved a level in would leave three columns or fewer, and breaks with four or more.
    /// </summary>
    [Fact]
    public void AHeldCallsPatternBody_StaysBesideTheArrowWhereItWouldNearlyFillTheLineBelow() =>
        Oracle.Agrees(
            """
            class C {
                void M841() {
                    IEnumerable<int> nnnnnnnnnnnnnnnnn = rrrrrrrrrrrr.OrderBy(x => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCC).TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();
                }
                void M1405() {
                    var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrr.OrderBy(x => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCC).TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();
                }
                void M392() {
                    if (b) {
                        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnn = rrr.Any(x => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB).TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();
                    }
                }
            }
            """,
            """
            class C {
                void M841() {
                    IEnumerable<int> nnnnnnnnnnnnnnnnn = rrrrrrrrrrrr
                        .OrderBy(x => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                            or BBBBBBBBBBBBBBBBBBBBBBBBBBBBB
                            or CCCCCCCCCCCCCCCCCCCCCCCCCCCCC
                        )
                        .TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();
                }

                void M1405() {
                    var nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn = rrrrrrrrrrr
                        .OrderBy(x =>
                            x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAA or BBBBBBBBBBBBBBBBBBBBBBBBBBBBB or CCCCCCCCCCCCCCCCCCCCCCCCCCCCC
                        )
                        .TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();
                }

                void M392() {
                    if (b) {
                        IEnumerable<int> nnnnnnnnnnnnnnnnnnnnnnnn =
                            rrr.Any(x => x is AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
                                    or BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB
                                )
                                .TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT();
                    }
                }
            }
            """
        );
}
