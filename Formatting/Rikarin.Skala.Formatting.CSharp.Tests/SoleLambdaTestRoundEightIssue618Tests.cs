namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #618, SK-DIV-0520 round eight: within sixteen points past the call of two's line the earlier rounds decide, a
///     one-argument call short of column 114 is decided by the call of two's line at its own constant, and a held first
///     call beside a kept arrow moves below its receiver by a line of its own. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output, from round seven's grids and this round's probes.
/// </summary>
public sealed class SoleLambdaTestRoundEightIssue618Tests {
    /// <summary>
    ///     Three members from 0 to 15 points past the line that the oracle keeps (the lattice tie), and one 21 points past
    ///     it that it breaks: the band ends at sixteen.
    /// </summary>
    [Fact]
    public void ACallOfTwoWithinTheLatticeBand_KeepsAsTheEarlierRoundsDo() =>
        Oracle.Agrees(
            """
            class C {
                object M2() {
                    if (c0) { T v = Transform(static node => Compute(hotelcharlieJulietHotelJulietgolfNameoscarindiaSymbolJuli, julietPapakiloNovemberBrav).Items.AsSpan()
            as string); }
                }
                object M21() {
                    T v = Attempt((a, b, c) => state.Store.Lookup(charlieBravoPapakiloechoValueindiaSym, alphagolfNamePapagolfName, Julietos)
            is not { });
                }
                object M508() {
                    Zabcdefghijk(xyyyyyyyyyyyyy => model.Zabcdefghijkl(charlieechoValuegolfNameechoValueJu, foxtrotkiloindiaSymbolmikeNodealph)
            is not null);
                }
                async void M74() {
                    if (c0) { await q.Visit(static x => Normalize(DeltaDeltaNovem, AlphaHotelechoValueJulietNovemberoscarechoValueJulietalph)
            is { IsStatic: true }); }
                }
            }
            """,
            """
            class C {
                object M2() {
                    if (c0) {
                        T v = Transform(static node => Compute(
                                    hotelcharlieJulietHotelJulietgolfNameoscarindiaSymbolJuli,
                                    julietPapakiloNovemberBrav
                                )
                                .Items.AsSpan()
                            as string
                        );
                    }
                }

                object M21() {
                    T v = Attempt((a, b, c) => state.Store.Lookup(
                            charlieBravoPapakiloechoValueindiaSym,
                            alphagolfNamePapagolfName,
                            Julietos
                        )
                        is not { }
                    );
                }

                object M508() {
                    Zabcdefghijk(xyyyyyyyyyyyyy => model.Zabcdefghijkl(
                            charlieechoValuegolfNameechoValueJu,
                            foxtrotkiloindiaSymbolmikeNodealph
                        )
                        is not null
                    );
                }

                async void M74() {
                    if (c0) {
                        await q.Visit(static x =>
                            Normalize(DeltaDeltaNovem, AlphaHotelechoValueJulietNovemberoscarechoValueJulietalph)
                                is { IsStatic: true }
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Two members a point under round seven's line that the oracle breaks (e = 110 and 111), and the keep and the break
    ///     nearest the line, at 1 000·(c + t) − 576·p − 605·h − 1 500·(i + a) = −157 266 and −157 194.
    /// </summary>
    [Fact]
    public void AOneArgumentCallShortOf114_IsTheCallOfTwosLine() =>
        Oracle.Agrees(
            """
            class C {
                object M303() {
                    if (c0) { Apply((int i) => root.Bind(CharlieBravoHotelgolfNamekiloLimaDeltaNovemberNovemberNovemberDeltaBravoBravokiloFoxtrotos)
            is IMethodSymbol); }
                }
                object M64() {
                    Where(static x => factory.Create(echoValuecharliekiloPapaHotelHotelgolfNameBravoPapaDeltaHotelindiaSymbolgolfNameechoValuemikeNo).Name
            is IMethodSymbol);
                }
                object M2196() {
                    items.TryFind((x, y) => context.Workspace.Get(alphaPapaechoValuecharliealphaNovemberechoValueLimaBravooscarindiaSymbolNovemberDeltaJulietecho)
            is SyntaxNode);
                }
                object M1375() {
                    if (c0) { if (c1) { if (c2) { return Find(item => this.cache.Lookup(JulietechoValueJulietcharliePapaDeltaLimaBravoHotelBravoFoxtrotindiaSymbolkilocharli)
            is IMethodSymbol); } } }
                }
            }
            """,
            """
            class C {
                object M303() {
                    if (c0) {
                        Apply((int i) =>
                            root.Bind(CharlieBravoHotelgolfNamekiloLimaDeltaNovemberNovemberNovemberDeltaBravoBravokiloFoxtrotos)
                                is IMethodSymbol
                        );
                    }
                }

                object M64() {
                    Where(static x =>
                        factory.Create(
                                    echoValuecharliekiloPapaHotelHotelgolfNameBravoPapaDeltaHotelindiaSymbolgolfNameechoValuemikeNo
                                )
                                .Name
                            is IMethodSymbol
                    );
                }

                object M2196() {
                    items.TryFind((x, y) => context.Workspace.Get(
                            alphaPapaechoValuecharliealphaNovemberechoValueLimaBravooscarindiaSymbolNovemberDeltaJulietecho
                        )
                        is SyntaxNode
                    );
                }

                object M1375() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                return Find(item =>
                                    this.cache.Lookup(
                                            JulietechoValueJulietcharliePapaDeltaLimaBravoHotelBravoFoxtrotindiaSymbolkilocharli
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
    ///     Two moves #528's table chopped (290 and 300 points past the line) and two chops it moved (330 and 280 short of
    ///     it).
    /// </summary>
    [Fact]
    public void AHeldCallBesideAKeptArrow_MovesByItsOwnLine() =>
        Oracle.Agrees(
            """
            class C {
                object M958() {
                    if (c0) { Select(x => this.GetSymbolInfo(deltaLimaBravomikeNodealphaalphakilomikeNodeoscarLimaFoxtrotcharlieechoValueosca).Value
            is not null); }
                }
                object M988() {
                    if (c0) { if (c1) { Apply(n => this.cache.Bind(LimaLimagolfNameNovemberalphaalphaindiaSymboloscarFoxtrotoscarBravomikeNodeFoxtrotm).Result.Name
            is IMethodSymbol); } }
                }
                object M916() {
                    Run(s => this.Model.Cache.Get(alphaechoValueNovemberDeltaNovemberalphakiloLimaNovemberPapaLimaHotelcharlieindiaSymbolechoVa).Result.Name
            is SyntaxNode);
                }
                object M2021() {
                    if (c0) { Apply(x => symbol.Read(limacharlieDeltaechoValuegolfNameNovembercharlieFoxtrotNovembercharlieJulietcharlieoscar).Result.Name
            is { } v); }
                }
            }
            """,
            """
            class C {
                object M958() {
                    if (c0) {
                        Select(x => this
                                .GetSymbolInfo(deltaLimaBravomikeNodealphaalphakilomikeNodeoscarLimaFoxtrotcharlieechoValueosca)
                                .Value
                            is not null
                        );
                    }
                }

                object M988() {
                    if (c0) {
                        if (c1) {
                            Apply(n => this.cache
                                    .Bind(LimaLimagolfNameNovemberalphaalphaindiaSymboloscarFoxtrotoscarBravomikeNodeFoxtrotm)
                                    .Result.Name
                                is IMethodSymbol
                            );
                        }
                    }
                }

                object M916() {
                    Run(s => this.Model.Cache.Get(
                                alphaechoValueNovemberDeltaNovemberalphakiloLimaNovemberPapaLimaHotelcharlieindiaSymbolechoVa
                            )
                            .Result.Name
                        is SyntaxNode
                    );
                }

                object M2021() {
                    if (c0) {
                        Apply(x => symbol.Read(
                                    limacharlieDeltaechoValuegolfNameNovembercharlieFoxtrotNovembercharlieJulietcharlieoscar
                                )
                                .Result.Name
                            is { } v
                        );
                    }
                }
            }
            """
        );
}
