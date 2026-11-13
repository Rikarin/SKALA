namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #620, SK-DIV-0760 and SK-DIV-0761: a sole lambda whose whole body is a call of one argument or more —
///     links after it or not, a type test on one line after it or not — breaks its arrow, where the call does not fit
///     beside it, by one measured line over the links, the lambda's head, the call's head and the arguments' line; and
///     the call's arguments, once they break, sit two levels past the line under a chain body as under a statement.
///     Every expected string is <c>jb cleanupcode</c> 2025.2.6's own output, from the probes named in the entries.
/// </summary>
public sealed class SoleLambdaCallArrowIssue620Tests {
    /// <summary>
    ///     Under a kept arrow and under a broken one, a sole lambda's chain body opens its first call's arguments two
    ///     levels past the line and its <c>)</c> and links one (SK-DIV-0761).
    /// </summary>
    [Fact]
    public void AHuggedCallUnderAChain_StacksItsArgumentsTwoLevelsIn() =>
        Oracle.Agrees(
            """
            class C {
                object M63() {
                    if (c0) { Something(static x => this.model.Heeee(golfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelind.Value).Value); }
                }
                object M24() {
                    return items.Select(x => Heeeeeeeeeeeeeeeeeeeeee(deltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFo.Value).Pr());
                }
            }
            """,
            """
            class C {
                object M63() {
                    if (c0) {
                        Something(static x => this.model.Heeee(
                                golfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelind.Value
                            )
                            .Value
                        );
                    }
                }

                object M24() {
                    return items.Select(x =>
                        Heeeeeeeeeeeeeeeeeeeeee(
                                deltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFo.Value
                            )
                            .Pr()
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A one-argument call that fits neither beside the arrow nor below it: the arrow breaks while the links are wide
    ///     against the lambda and the head, <c>5·L ≥ 3·(λ + h)</c> (SK-DIV-0760).
    /// </summary>
    [Fact]
    public void AOneArgumentCallThatFitsNowhere_BreaksByTheMeasuredLine() =>
        Oracle.Agrees(
            """
            class C {
                object M33() {
                    if (c0) { var r = recv.Use((Node node) => this.Heeeeeeeeeee(indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalpha!).Prop); }
                }
                object M107() {
                    if (c0) { return items.Select((a, b) => Helpers.Heee(echoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfName).Propxxxxx); }
                }
                object M108() {
                    if (c0) { Use((a, b) => recv.He(GetRoot(golfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbo)).Name.Length); }
                }
                object M121() {
                    if (c0) { var r = recv.Use(x => recv.Heeeeeeee((int)hotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBra).Count); }
                }
            }
            """,
            """
            class C {
                object M33() {
                    if (c0) {
                        var r = recv.Use((Node node) => this.Heeeeeeeeeee(
                                indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalpha!
                            )
                            .Prop
                        );
                    }
                }

                object M107() {
                    if (c0) {
                        return items.Select((a, b) => Helpers.Heee(
                                echoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfName
                            )
                            .Propxxxxx
                        );
                    }
                }

                object M108() {
                    if (c0) {
                        Use((a, b) =>
                            recv.He(
                                    GetRoot(
                                        golfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbo
                                    )
                                )
                                .Name.Length
                        );
                    }
                }

                object M121() {
                    if (c0) {
                        var r = recv.Use(x => recv.Heeeeeeee(
                                (int)
                                hotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBra
                            )
                            .Count
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Two arguments or more: the arrow breaks only while the links outweigh the lambda, the head and the arguments'
    ///     line (SK-DIV-0760).
    /// </summary>
    [Fact]
    public void ACallOfTwoArgumentsOrMore_BreaksByTheMeasuredLine() =>
        Oracle.Agrees(
            """
            class C {
                object M24() {
                    Use((Node node) => Heee(alphaBravocharl.Value, bravocharlieDeltaecho, (int)charlieDeltaecho, deltaechoValueF.Value).Pr());
                }
                object M34() {
                    _ = Enumerate((x) => this.model.Heeeeeeee("bravocharlieDelt", charlieDeltaechoVa, x.Deltaechovaluefo, echoValueFoxtrotgo).Select(y => y.Name));
                }
                object M58() {
                    if (c0) { TryFind(node => Heeeeeeee<int>("bravocharlieDeltaechoValueFoxtrotgolfNameH", x.Charliedeltaechovaluefoxtrotgolfnamehoteli).Select(y => y.Name)); }
                }
                object M59() {
                    if (c0) { _ = Enumerate(node => Heeeeeeeeeeeeeeeeeeeeee(foxtrotgolfNameHot!, golfNameHotelindiaS, hotelindiaSymbolJu!, GetRoot(indiaSymbo)).First().Name); }
                }
            }
            """,
            """
            class C {
                object M24() {
                    Use((Node node) => Heee(
                            alphaBravocharl.Value,
                            bravocharlieDeltaecho,
                            (int)charlieDeltaecho,
                            deltaechoValueF.Value
                        )
                        .Pr()
                    );
                }

                object M34() {
                    _ = Enumerate((x) =>
                        this.model.Heeeeeeee("bravocharlieDelt", charlieDeltaechoVa, x.Deltaechovaluefo, echoValueFoxtrotgo)
                            .Select(y => y.Name)
                    );
                }

                object M58() {
                    if (c0) {
                        TryFind(node => Heeeeeeee<int>(
                                "bravocharlieDeltaechoValueFoxtrotgolfNameH",
                                x.Charliedeltaechovaluefoxtrotgolfnamehoteli
                            )
                            .Select(y => y.Name)
                        );
                    }
                }

                object M59() {
                    if (c0) {
                        _ = Enumerate(node => Heeeeeeeeeeeeeeeeeeeeee(
                                foxtrotgolfNameHot!,
                                golfNameHotelindiaS,
                                hotelindiaSymbolJu!,
                                GetRoot(indiaSymbo)
                            )
                            .First()
                            .Name
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A type test written on one line over such a call: the test is one more link, its width with the rest
    ///     (SK-DIV-0760).
    /// </summary>
    [Fact]
    public void ATypeTestOnOneLine_ReadsTheTestAsALink() =>
        Oracle.Agrees(
            """
            class C {
                object M115() {
                    if (c0) { TryFind(x => Normalize(GetRoot(novemberechoValueHoteloscarFoxtrotHotelPapakiloBravooscarcharlieJulietDeltaFoxtrotmikePa)) as Node); }
                }
                object M1168() {
                    if (c0) { Use(x => this.cache.Head(deltaalphaoscargolfNamePapaFoxtrotPapamikekiloHotelalphaalphaDeltaNovembermikeBravoLimaos) is null); }
                }
                object M242() {
                    this.Apply((a, b) => Open("foxtrotindiaSymbolHotelcharlie", oscarFoxtrotHotelmikeHoteloscar, golfNameLimaFoxtrotcharliegolfNa!) is null);
                }
            }
            """,
            """
            class C {
                object M115() {
                    if (c0) {
                        TryFind(x => Normalize(
                                GetRoot(novemberechoValueHoteloscarFoxtrotHotelPapakiloBravooscarcharlieJulietDeltaFoxtrotmikePa)
                            ) as Node
                        );
                    }
                }

                object M1168() {
                    if (c0) {
                        Use(x => this.cache.Head(
                                deltaalphaoscargolfNamePapaFoxtrotPapamikekiloHotelalphaalphaDeltaNovembermikeBravoLimaos
                            ) is null
                        );
                    }
                }

                object M242() {
                    this.Apply((a, b) => Open(
                            "foxtrotindiaSymbolHotelcharlie",
                            oscarFoxtrotHotelmikeHoteloscar,
                            golfNameLimaFoxtrotcharliegolfNa!
                        ) is null
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A break the author wrote after the arrow is kept, where the same body written on one line keeps the arrow;
    ///     and one the author wrote inside the call, or before a link, keeps the arrow where the line would break it.
    /// </summary>
    [Fact]
    public void TheAuthorsBreaks_DecideTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M1() {
                    Something(abc =>
                        Heeeeeeeeeeeeee(indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrot).Prr);
                }
                object M6() {
                    Something(abc =>
                        Heeeeeeeeeeeeee(indiaSymbolJulietalphaBravocharlieDelta, echoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrot).Prr);
                }
                object M3() {
                    Something(abc => Heeeeeeeeeeeeee(
                        indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrot).Prrrrrrrrrrrrrrrrrrrrrrr);
                }
                object M5() {
                    Something(abc => Heeeeeeeeeeeeee(indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrot)
                        .Prrrrrrrrrrrrrrrrrrrrrrr);
                }
            }
            """,
            """
            class C {
                object M1() {
                    Something(abc =>
                        Heeeeeeeeeeeeee(
                                indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrot
                            )
                            .Prr
                    );
                }

                object M6() {
                    Something(abc =>
                        Heeeeeeeeeeeeee(
                                indiaSymbolJulietalphaBravocharlieDelta,
                                echoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrot
                            )
                            .Prr
                    );
                }

                object M3() {
                    Something(abc => Heeeeeeeeeeeeee(
                            indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrot
                        )
                        .Prrrrrrrrrrrrrrrrrrrrrrr
                    );
                }

                object M5() {
                    Something(abc => Heeeeeeeeeeeeee(
                            indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrot
                        )
                        .Prrrrrrrrrrrrrrrrrrrrrrr
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A chain of properties that fits neither beside the arrow nor below it: the arrow breaks once the lambda starts
    ///     far enough in for its parameters, 100·s − 175·p − 12·f ≥ 776 (SK-DIV-0762).
    /// </summary>
    [Fact]
    public void APropertyChainThatFitsNowhere_BreaksOnceTheLambdaStartsFarEnoughIn() =>
        Oracle.Agrees(
            """
            class C {
                object M29() {
                    var symbols = context.Compilation.Use((x) => symbol.Charliebr.Deltabravolima.Foxtrotkilopapami.Indiasymboljulie.Papamikepapaindi.Papabravonovem.Novemb.Hotel);
                }
                object M85() {
                    if (c0) { bool ok = nodes.All((x, y) => x.Charliedeltai.Foxtrotmikemikegol.Foxtrot.Lima.Juli.Oscarpapanovem.Hotellimajulietju.Kilopapaindiasymbo.Charlienovember); }
                }
                object M1100() {
                    if (c0) { this.Apply(symbol => x.Oscarbravo.Bravo.Julietch.India.Novemberkiloind.Julietindiasymbolk.Alphabravopapap.Charli.Charliedeltad.Bravogolfna.Deltapapadel.Limafoxtrotjul); }
                }
                object M1324() {
                    if (c0) { bool ok = nodes.All(static x => symbol.Papa.Alphacharl.Alphaalphaech.Limagolfnamedel.Oscarhotel.Foxtrotindi.Echovalueli.Indiasymbo.Charliek.Novemberjul); }
                }
                object M1359() {
                    if (c0) { return items.Select((x, y) => symbol.Hotelcharliedel.Echovalu.Echovalueindiasymb.Alphadelt.Papagolfnamelimae.Kilodeltaoscarec.Charlie.Julietlimaalphaho); }
                }
            }
            """,
            """
            class C {
                object M29() {
                    var symbols = context.Compilation.Use((x) =>
                        symbol.Charliebr.Deltabravolima.Foxtrotkilopapami.Indiasymboljulie.Papamikepapaindi.Papabravonovem.Novemb
                            .Hotel
                    );
                }

                object M85() {
                    if (c0) {
                        bool ok = nodes.All((x, y) =>
                            x.Charliedeltai.Foxtrotmikemikegol.Foxtrot.Lima.Juli.Oscarpapanovem.Hotellimajulietju.Kilopapaindiasymbo
                                .Charlienovember
                        );
                    }
                }

                object M1100() {
                    if (c0) {
                        this.Apply(symbol => x.Oscarbravo.Bravo.Julietch.India.Novemberkiloind.Julietindiasymbolk.Alphabravopapap
                            .Charli.Charliedeltad.Bravogolfna.Deltapapadel.Limafoxtrotjul
                        );
                    }
                }

                object M1324() {
                    if (c0) {
                        bool ok = nodes.All(static x =>
                            symbol.Papa.Alphacharl.Alphaalphaech.Limagolfnamedel.Oscarhotel.Foxtrotindi.Echovalueli.Indiasymbo
                                .Charliek.Novemberjul
                        );
                    }
                }

                object M1359() {
                    if (c0) {
                        return items.Select((x, y) =>
                            symbol.Hotelcharliedel.Echovalu.Echovalueindiasymb.Alphadelt.Papagolfnamelimae.Kilodeltaoscarec.Charlie
                                .Julietlimaalphaho
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A head of forty columns or more behind an arrow far to the right breaks the arrow against the arguments' line,
    ///     20·a + 6·h − 14·λ + 23·L − 34·E ≥ −2214 (SK-DIV-0760, round two).
    /// </summary>
    [Fact]
    public void AWideHeadBehindAFarArrow_BreaksAgainstTheArgumentsLine() =>
        Oracle.Agrees(
            """
            class C {
                object M24() {
                    SomethingElseLongerHere(x => Heeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindi).Prrrrrr);
                }
                object M80() {
                    SomethingElseLongerHere(x => Heeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameH).Prrrrrr);
                }
            }
            """,
            """
            class C {
                object M24() {
                    SomethingElseLongerHere(x =>
                        Heeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(
                                indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindi
                            )
                            .Prrrrrr
                    );
                }

                object M80() {
                    SomethingElseLongerHere(x =>
                        Heeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(
                                indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameH
                            )
                            .Prrrrrr
                    );
                }
            }
            """
        );
}
