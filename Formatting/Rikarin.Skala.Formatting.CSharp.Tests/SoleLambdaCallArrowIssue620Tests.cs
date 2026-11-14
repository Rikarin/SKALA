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

    /// <summary>
    ///     A whole argument <c>name!</c> whose line runs past the margin breaks before its <c>!</c>, on the argument's
    ///     own column, even where the name alone overflows (SK-DIV-0810).
    /// </summary>
    [Fact]
    public void AWholeArgumentsBangPastTheMargin_BreaksBeforeIt() =>
        Oracle.Agrees(
            """
            class C {
                object M38() {
                    if (c0) { if (c1) { Use(x => Head(julietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindi!)); } }
                }
                object M367() {
                    if (c0) { if (c1) { var v = Head(foxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravo!); } }
                }
                object M1370() {
                    if (c0) { if (c1) { Use(x => Head(foxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJuliet!, other)); } }
                }
            }
            """,
            """
            class C {
                object M38() {
                    if (c0) {
                        if (c1) {
                            Use(x => Head(
                                    julietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindi
                                    !
                                )
                            );
                        }
                    }
                }

                object M367() {
                    if (c0) {
                        if (c1) {
                            var v = Head(
                                foxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravo
                                !
                            );
                        }
                    }
                }

                object M1370() {
                    if (c0) {
                        if (c1) {
                            Use(x => Head(
                                    foxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJuliet
                                    !,
                                    other
                                )
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     <c>x.Name</c> past the margin on a line of its own keeps its dot: the oracle never breaks after a one-column
    ///     name that starts its line (SK-DIV-0810).
    /// </summary>
    [Fact]
    public void AOneColumnReceiverStartingItsLine_KeepsItsDot() =>
        Oracle.Agrees(
            """
            class C {
                object M388() {
                    var v = Head(x.Foxtrotgolfnamehotelindiasymboljulietalphabravocharliedeltaechovaluefoxtrotgolfnamehotelindiasymboljulietalphabravo);
                }
                object M402() {
                    if (c0) { if (c1) { var v = Head(x.Bravocharliedeltaechovaluefoxtrotgolfnamehotelindiasymboljulietalphabravocharliedeltaechovaluefoxtrot); } }
                }
            }
            """,
            """
            class C {
                object M388() {
                    var v = Head(
                        x.Foxtrotgolfnamehotelindiasymboljulietalphabravocharliedeltaechovaluefoxtrotgolfnamehotelindiasymboljulietalphabravo
                    );
                }

                object M402() {
                    if (c0) {
                        if (c1) {
                            var v = Head(
                                x.Bravocharliedeltaechovaluefoxtrotgolfnamehotelindiasymboljulietalphabravocharliedeltaechovaluefoxtrot
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A one-argument call under a type test written on one line keeps its arguments whole and breaks after the
    ///     keyword, or before it where the keyword does not fit, by <c>100·g + 40·h + 116·s − 67·t ≤ 9633</c>; at 121 columns
    ///     with its <c>;</c> the list chops (SK-DIV-0811).
    /// </summary>
    [Fact]
    public void ATestOverAOneArgumentCall_TakesTheBreakByTheMeasuredLine() =>
        Oracle.Agrees(
            """
            class C {
                object M322() {
                    ok = Heeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(julietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymb) as Node;
                }
                object M1146() {
                    if (c0) { if (c1) { ok = Heeeeeeeeeeeeeeeeeeeeee(foxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxt) as Node; } }
                }
                object M4880() {
                    if (c0) { if (c1) { ok = Heeeeeeeeeeeeeeeeeeeeeeeeeeee(deltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalp) is SyntaxNode; } }
                }
            }
            """,
            """
            class C {
                object M322() {
                    ok = Heeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(julietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymb) as
                        Node;
                }

                object M1146() {
                    if (c0) {
                        if (c1) {
                            ok = Heeeeeeeeeeeeeeeeeeeeee(foxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxt)
                                as Node;
                        }
                    }
                }

                object M4880() {
                    if (c0) {
                        if (c1) {
                            ok = Heeeeeeeeeeeeeeeeeeeeeeeeeeee(
                                deltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalp
                            ) is SyntaxNode;
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     The test's own gaps are one fill: <c>… is SyntaxNode</c> / <c>node</c> and <c>… is not</c> /
    ///     <c>SyntaxNode</c>, the rightmost that fits (SK-DIV-0811).
    /// </summary>
    [Fact]
    public void ATestsOwnGaps_AreOneFill() =>
        Oracle.Agrees(
            """
            class C {
                object M1681() {
                    return this.Heeeeeeeeee(indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbol) is SyntaxNode node;
                }
                object M3295() {
                    if (c0) { if (c1) { _ = recv.Heeeeeeeeeeeeeeeeeeeeeeee(hotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtro) is not SyntaxNode; } }
                }
            }
            """,
            """
            class C {
                object M1681() {
                    return this.Heeeeeeeeee(indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbol) is
                        SyntaxNode node;
                }

                object M3295() {
                    if (c0) {
                        if (c1) {
                            _ = recv.Heeeeeeeeeeeeeeeeeeeeeeee(hotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtro) is not
                                SyntaxNode;
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Under a broken arrow the same line decides with its own constant, and the test's break sits one level past the
    ///     body's line (SK-DIV-0811).
    /// </summary>
    [Fact]
    public void ATestUnderABrokenArrow_TakesTheBreakOneLevelIn() =>
        Oracle.Agrees(
            """
            class C {
                object M2372() {
                    Use(static (a, b) => recv.Heeeeeeeeeeeeeeeeeeeeeeee(echoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFox) is SyntaxNode);
                }
                object M2713() {
                    var r = recv.Use(static x => Open(foxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravoch) as Node);
                }
                object M2919() {
                    if (c0) { if (c1) { var r = recv.Use(static x => this.cache.Open(indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaS) is SyntaxNode); } }
                }
            }
            """,
            """
            class C {
                object M2372() {
                    Use(static (a, b) =>
                        recv.Heeeeeeeeeeeeeeeeeeeeeeee(
                            echoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFox
                        ) is SyntaxNode
                    );
                }

                object M2713() {
                    var r = recv.Use(static x => Open(
                            foxtrotgolfNameHotelindiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaSymbolJulietalphaBravoch
                        ) as Node
                    );
                }

                object M2919() {
                    if (c0) {
                        if (c1) {
                            var r = recv.Use(static x =>
                                this.cache.Open(indiaSymbolJulietalphaBravocharlieDeltaechoValueFoxtrotgolfNameHotelindiaS) is
                                    SyntaxNode
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A chain body whose first call fits beside the arrow and which fits nowhere whole: the arrow stays while
    ///     <c>200·h ≥ 72·s − 127·p + 4067</c> and <c>200·s − 352·p − 25·h ≤ 1441</c>, and breaks otherwise (SK-DIV-0812).
    /// </summary>
    [Fact]
    public void AChainWhoseFirstCallFitsBesideTheArrow_KeepsItByTwoLines() =>
        Oracle.Agrees(
            """
            class C {
                object M1321() {
                    Ussssssssssssssssss((Node node) => this.Foooooooooooooooo(item).Analyze(indiaSymbolJulietalphaBravocharlieDelta, bravocharlieDeltaechoValueFoxtrotgolfNa));
                }
                object M1325() {
                    Ussssssssssssssssss((Node node) => this.Fooooooooooooooooooo(item).Analyze(foxtrotgolfNameHotelindiaSymbolJuliet, indiaSymbolJulietalphaBravocharlieDelt));
                }
                object M2469() {
                    if (c0) { if (c1) { U(static (Node node) => Foooooo().Analyze(hotelindiaSymbolJulietalphaBravocharlieDeltaecho, alphaBravocharlieDeltaechoValueFoxtrotgolfNameHot)); } }
                }
                object M3777() {
                    if (c0) { if (c1) { Ussssssssssssss(static x => this.Foooooooooo(item).Analyze(echoValueFoxtrotgolfNameHotelindiaSymbolJu, hotelindiaSymbolJulietalphaBravocharlieDel)); } }
                }
            }
            """,
            """
            class C {
                object M1321() {
                    Ussssssssssssssssss((Node node) => this.Foooooooooooooooo(item)
                        .Analyze(indiaSymbolJulietalphaBravocharlieDelta, bravocharlieDeltaechoValueFoxtrotgolfNa)
                    );
                }

                object M1325() {
                    Ussssssssssssssssss((Node node) => this.Fooooooooooooooooooo(item)
                        .Analyze(foxtrotgolfNameHotelindiaSymbolJuliet, indiaSymbolJulietalphaBravocharlieDelt)
                    );
                }

                object M2469() {
                    if (c0) {
                        if (c1) {
                            U(static (Node node) =>
                                Foooooo()
                                    .Analyze(
                                        hotelindiaSymbolJulietalphaBravocharlieDeltaecho,
                                        alphaBravocharlieDeltaechoValueFoxtrotgolfNameHot
                                    )
                            );
                        }
                    }
                }

                object M3777() {
                    if (c0) {
                        if (c1) {
                            Ussssssssssssss(static x =>
                                this.Foooooooooo(item)
                                    .Analyze(echoValueFoxtrotgolfNameHotelindiaSymbolJu, hotelindiaSymbolJulietalphaBravocharlieDel)
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     On the line below the same two lines keep the arrow only once that line would end at column 112 or later, and
    ///     never before 108 (SK-DIV-0812).
    /// </summary>
    [Fact]
    public void AChainThatFitsBelow_KeepsItsArrowOnlyNearTheMargin() =>
        Oracle.Agrees(
            """
            class C {
                object M15887() {
                    if (c0) { if (c1) { Ussssss(static (Node node) => model.Normalize(item).Merge(deltaechoValueFox, echoValueFoxtrotg, foxtrotgolfNameHo).Single()); } }
                }
                object M10444() {
                    if (c0) { Ussssssssss((x, y) => recv.LookupTheDeclaration().Merge(julietalphaBravocharlieDeltaechoV, alphaBravocharlieDeltaechoValueFo)); }
                }
            }
            """,
            """
            class C {
                object M15887() {
                    if (c0) {
                        if (c1) {
                            Ussssss(static (Node node) => model.Normalize(item)
                                .Merge(deltaechoValueFox, echoValueFoxtrotg, foxtrotgolfNameHo)
                                .Single()
                            );
                        }
                    }
                }

                object M10444() {
                    if (c0) {
                        Ussssssssss((x, y) =>
                            recv.LookupTheDeclaration().Merge(julietalphaBravocharlieDeltaechoV, alphaBravocharlieDeltaechoValueFo)
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A <c>var</c> local's <c>=</c> before a sole lambda call breaks where the value fits below by one line in the
    ///     callee, the name, the indent and the parameters, the body playing no part — on a line two columns over too
    ///     (SK-DIV-0880).
    /// </summary>
    [Fact]
    public void AWideCalleesEquals_BreaksWhereTheValueFitsBelow() =>
        Oracle.Agrees(
            """
            class C {
                object M574() {
                    var symbols = context.Compilation.Use(node => Helpers.Normalize(novemberBravoBravokiloDeltagolfNameL!).ToString());
                }
                object M3620() {
                    if (c0) { if (c1) { if (c2) { var symbols = context.Compilation.Use(symbol => Analyze(golfNameki, bravoPapaalp) is not SyntaxNode); } } }
                }
            }
            """,
            """
            class C {
                object M574() {
                    var symbols =
                        context.Compilation.Use(node => Helpers.Normalize(novemberBravoBravokiloDeltagolfNameL!).ToString());
                }

                object M3620() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                var symbols =
                                    context.Compilation.Use(symbol => Analyze(golfNameki, bravoPapaalp) is not SyntaxNode);
                            }
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     On the line below a chain's arrow stays behind a long receiver and before a property as before a call of two
    ///     arguments, and short of column 112 by one more line in the parameters, the first call and the start
    ///     (SK-DIV-0881).
    /// </summary>
    [Fact]
    public void AChainThatFitsBelow_KeepsItsArrowByTheBandsLine() =>
        Oracle.Agrees(
            """
            class C {
                object M453() {
                    Uss(static (x, y) => alphaBravocharlieDeltaechoValueFoxtrotg.Head(item).With(julietalphaBravocha, alphaBravocharlieDe));
                }
                object M220() {
                    Uss((Node node) => alphaBravocharlieDe.Head(item).With(indiaSymbolJulietalphaBravoc, julietalphaBravocharlieDeltae));
                }
                object M6291() {
                    if (c0) { if (c1) { Ussssssssss(static (Node node) => alphaBravocharlieDe.Head(item).With(charlieDeltaechoValueFoxtr, deltaechoValueFoxtrotgolfNa)); } }
                }
            }
            """,
            """
            class C {
                object M453() {
                    Uss(static (x, y) => alphaBravocharlieDeltaechoValueFoxtrotg.Head(item)
                        .With(julietalphaBravocha, alphaBravocharlieDe)
                    );
                }

                object M220() {
                    Uss((Node node) =>
                        alphaBravocharlieDe.Head(item).With(indiaSymbolJulietalphaBravoc, julietalphaBravocharlieDeltae)
                    );
                }

                object M6291() {
                    if (c0) {
                        if (c1) {
                            Ussssssssss(static (Node node) => alphaBravocharlieDe.Head(item)
                                .With(charlieDeltaechoValueFoxtr, deltaechoValueFoxtrotgolfNa)
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A chain of properties that fits below keeps its arrow by the same lines, the fill's width for the first call's
    ///     (SK-DIV-0881).
    /// </summary>
    [Fact]
    public void APropertyChainThatFitsBelow_KeepsItsArrowByTheChainsLines() =>
        Oracle.Agrees(
            """
            class C {
                object M655() {
                    Uss(static (Node node) => x.Hoteli.Indias.Juliet.Alphab.Bravoc.Charli.Deltae.Echova.Foxtro.Golfna.Hoteli.Indias.Juliet.Ho);
                }
                object M1169() {
                    Ussssss((x, y) => node.Alphab.Bravoc.Charli.Deltae.Echova.Foxtro.Golfna.Hoteli.Indias.Juliet.Alphab.Bravoc.Charli.Al);
                }
                object M3420() {
                    Ussssssssss(static (x, y) => this.model.Foxtrotgo.Golfnameh.Hotelindi.Indiasymb.Julietalp.Alphabrav.Bravochar.Charliede.Deltaecho.Fo);
                }
            }
            """,
            """
            class C {
                object M655() {
                    Uss(static (Node node) => x.Hoteli.Indias.Juliet.Alphab.Bravoc.Charli.Deltae.Echova.Foxtro.Golfna.Hoteli.Indias
                        .Juliet.Ho
                    );
                }

                object M1169() {
                    Ussssss((x, y) =>
                        node.Alphab.Bravoc.Charli.Deltae.Echova.Foxtro.Golfna.Hoteli.Indias.Juliet.Alphab.Bravoc.Charli.Al
                    );
                }

                object M3420() {
                    Ussssssssss(static (x, y) => this.model.Foxtrotgo.Golfnameh.Hotelindi.Indiasymb.Julietalp.Alphabrav.Bravochar
                        .Charliede.Deltaecho.Fo
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A type test the author broke before its keyword is #618's, not SK-DIV-0811's one-line rule (coordinator's #618
    ///     probe, seed 990618).
    /// </summary>
    [Fact]
    public void ATestBrokenBeforeItsKeyword_IsNotTheOneLineRules() =>
        Oracle.Agrees(
            """
            class C {
                async void M59() {
                    if (c0) { await q.Select(item => ctx.Resolve(MikeNodekiloHotelLimagolfNameFoxtrotNovemberalphaDeltaBravoHote)
            is SyntaxNode); }
                }
            }
            """,
            """
            class C {
                async void M59() {
                    if (c0) {
                        await q.Select(item => ctx.Resolve(MikeNodekiloHotelLimagolfNameFoxtrotNovemberalphaDeltaBravoHote)
                            is SyntaxNode
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A lone two-argument call behind a lead of 35 keeps its arrow; at a 9-column lambda two bands (SK-DIV-0880).
    /// </summary>
    [Fact]
    public void AShortLeadOfThirtyFive_KeepsTheArrowAndTheBandsAtNine() =>
        Oracle.Agrees(
            """
            class C {
                object M574() {
                    var symbolsOfIt = context.Use(x => Helpers.LookupTheDeclarationOf(bravocharlieDeltaechoValueFo, charlieDeltaechoValueFoxtrot));
                }
                object M248() {
                    Usssssssssssssss((x, y) => Helpers.LookupTheDeclarat(deltaechoValueFoxtrotgolfNameH, echoValueFoxtrotgolfNameHoteli));
                }
                object M278() {
                    Ussssssssssssssss((x, y) => Helpers.LookupTheDeclarat(echoValueFoxtrotgolfNameHoteli, foxtrotgolfNameHotelindiaSymbo));
                }
                object M844() {
                    Usssssssssssssssssssssssssssssss((x, y) => Helpers.LookupTheDeclarat(julietalphaBravocharlieDeltaec, alphaBravocharlieDeltaechoValu));
                }
                object M1069() {
                    Ussssssssssssssssssssssssssssssssssss((x, y) => Helpers.LookupTheDeclarat(echoValueFoxtrotgolfNameHoteli, foxtrotgolfNameHotelindiaSymbo));
                }
                object M1473() {
                    Ussssssssssssssssssssssssssssssssssssssssssss(abcdefgh => Helpers.LookupTheDec(charlieDeltaechoValueFoxtrotgo, deltaechoValueFoxtrotgolfNameH));
                }
                object M1533() {
                    Usssssssssssssssssssssssssssssssssssssssssssss(abcdefgh => Helpers.LookupTheDec(deltaechoValueFoxtrotgolfNameH, echoValueFoxtrotgolfNameHoteli));
                }
            }
            """,
            """
            class C {
                object M574() {
                    var symbolsOfIt = context.Use(x => Helpers.LookupTheDeclarationOf(
                            bravocharlieDeltaechoValueFo,
                            charlieDeltaechoValueFoxtrot
                        )
                    );
                }

                object M248() {
                    Usssssssssssssss((x, y) => Helpers.LookupTheDeclarat(
                            deltaechoValueFoxtrotgolfNameH,
                            echoValueFoxtrotgolfNameHoteli
                        )
                    );
                }

                object M278() {
                    Ussssssssssssssss((x, y) =>
                        Helpers.LookupTheDeclarat(echoValueFoxtrotgolfNameHoteli, foxtrotgolfNameHotelindiaSymbo)
                    );
                }

                object M844() {
                    Usssssssssssssssssssssssssssssss((x, y) => Helpers.LookupTheDeclarat(
                            julietalphaBravocharlieDeltaec,
                            alphaBravocharlieDeltaechoValu
                        )
                    );
                }

                object M1069() {
                    Ussssssssssssssssssssssssssssssssssss((x, y) =>
                        Helpers.LookupTheDeclarat(echoValueFoxtrotgolfNameHoteli, foxtrotgolfNameHotelindiaSymbo)
                    );
                }

                object M1473() {
                    Ussssssssssssssssssssssssssssssssssssssssssss(abcdefgh => Helpers.LookupTheDec(
                            charlieDeltaechoValueFoxtrotgo,
                            deltaechoValueFoxtrotgolfNameH
                        )
                    );
                }

                object M1533() {
                    Usssssssssssssssssssssssssssssssssssssssssssss(abcdefgh =>
                        Helpers.LookupTheDec(deltaechoValueFoxtrotgolfNameH, echoValueFoxtrotgolfNameHoteli)
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A one-argument call under a one-line property pattern breaks before its is, or inside its braces (SK-DIV-0763).
    /// </summary>
    [Fact]
    public void ABracedTypeTest_BreaksBeforeItsIsOrInsideItsBraces() =>
        Oracle.Agrees(
            """
            class C {
                object M340() {
                    var r = recv.Use(x => recv.Analyze(limaechoValuegolfNameoscarNovemberFoxtrotFoxtrotechoValuecharliemikealphaNovemberBravoosc) is IMethodSymbol { IsStatic: true });
                }
                object M205() {
                    return items.Select(node => Read(oscarkilomikeechoValuegolfNameNovemberHotelFoxtrotNovembergolfNameHotelPapaNovember) is { IsStatic: true });
                }
                object M916() {
                    if (c0) { bool ok = nodes.All(x => Helpers.Head<INamedTypeSymbol>(indiaSymbolFoxtrotBravogolfNameNovemberPapaHotelDeltaechoValuePapa) is not { }); }
                }
                object M16() {
                    if (c0) { if (c1) { var r = recv.Use((x, y) => Helpers.LookupTheDeclaration(alphaJulietNovemberkilooscarLimaJulietcharlieJulietHotelindia) is IMethodSymbol { IsStatic: true }); } }
                }
                object M547() {
                    _ = Enumerate(static x => this.cache.GetTheSymbolOf(GetRoot(kilomikeDeltaPapagolfNameLimakiloDeltacharlieHotelJulietLimaJulietin)) is not { });
                }
            }
            """,
            """
            class C {
                object M340() {
                    var r = recv.Use(x =>
                        recv.Analyze(limaechoValuegolfNameoscarNovemberFoxtrotFoxtrotechoValuecharliemikealphaNovemberBravoosc)
                            is IMethodSymbol { IsStatic: true }
                    );
                }

                object M205() {
                    return items.Select(node =>
                        Read(oscarkilomikeechoValuegolfNameNovemberHotelFoxtrotNovembergolfNameHotelPapaNovember) is {
                            IsStatic: true
                        }
                    );
                }

                object M916() {
                    if (c0) {
                        bool ok = nodes.All(x =>
                            Helpers.Head<INamedTypeSymbol>(indiaSymbolFoxtrotBravogolfNameNovemberPapaHotelDeltaechoValuePapa) is
                                not { }
                        );
                    }
                }

                object M16() {
                    if (c0) {
                        if (c1) {
                            var r = recv.Use((x, y) =>
                                Helpers.LookupTheDeclaration(alphaJulietNovemberkilooscarLimaJulietcharlieJulietHotelindia)
                                    is IMethodSymbol { IsStatic: true }
                            );
                        }
                    }
                }

                object M547() {
                    _ = Enumerate(static x =>
                        this.cache.GetTheSymbolOf(GetRoot(kilomikeDeltaPapagolfNameLimakiloDeltacharlieHotelJulietLimaJulietin)) is
                            not { }
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A chain whose first call fits nowhere beside the arrow breaks it behind a short receiver (SK-DIV-0812).
    /// </summary>
    [Fact]
    public void AChainWhoseFirstCallDoesNotFitBesideTheArrow_BreaksItBehindAShortReceiver() =>
        Oracle.Agrees(
            """
            class C {
                object M770() {
                    return items.Select(static (x, y) => this.cache.Use<int>(julietJulietNovemberNovemberBravoBravokiloFoxtrotgolfNamemikegolfNameB).Analyze(GetRoot(alphaDeltaalphaDeltag), (int)bravoJulietLimaNovemberHotel).Select(y => y.Name));
                }
                object M1391() {
                    if (c0) { var r = recv.Use(static (x, y) => Helpers.Bind<INamedTypeSymbol>(GetRoot(indiaSymbolgolfNamePapaHoteloscarPa)).With(julietosca, echoValuei).Single()); }
                }
                object M923() {
                    if (c0) { if (c1) { if (c2) { var r = recv.Use(static (x, y) => this.model.LookupTheDeclaration(oscarBravoechoValuekiloBravoFoxtrotc).Select(novembe, brav, echoVa)); } } }
                }
                object M1130() {
                    if (c0) { var symbols = context.Compilation.Use(static x => this.Open(mikeLimakiloPapaalphaechoValueechoValueDeltakiloFoxt).Analyze(x.Indiasymbolpapaecho, novemberindiaSymbo, indiaSymbol.Value)); }
                }
                object M2273() {
                    if (c0) { if (c1) { if (c2) { bool ok = nodes.All(static (x, y) => recv.Use(x.Mikealphagolfnamekilojulietjulietkilocharliehotelgolf).Merge(limamikeindiaSymbolmik!, papaNovembergolfNameind.Value)); } } }
                }
            }
            """,
            """
            class C {
                object M770() {
                    return items.Select(static (x, y) =>
                        this.cache.Use<int>(julietJulietNovemberNovemberBravoBravokiloFoxtrotgolfNamemikegolfNameB)
                            .Analyze(GetRoot(alphaDeltaalphaDeltag), (int)bravoJulietLimaNovemberHotel)
                            .Select(y => y.Name)
                    );
                }

                object M1391() {
                    if (c0) {
                        var r = recv.Use(static (x, y) =>
                            Helpers.Bind<INamedTypeSymbol>(GetRoot(indiaSymbolgolfNamePapaHoteloscarPa))
                                .With(julietosca, echoValuei)
                                .Single()
                        );
                    }
                }

                object M923() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                var r = recv.Use(static (x, y) =>
                                    this.model.LookupTheDeclaration(oscarBravoechoValuekiloBravoFoxtrotc)
                                        .Select(novembe, brav, echoVa)
                                );
                            }
                        }
                    }
                }

                object M1130() {
                    if (c0) {
                        var symbols = context.Compilation.Use(static x =>
                            this.Open(mikeLimakiloPapaalphaechoValueechoValueDeltakiloFoxt)
                                .Analyze(x.Indiasymbolpapaecho, novemberindiaSymbo, indiaSymbol.Value)
                        );
                    }
                }

                object M2273() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                bool ok = nodes.All(static (x, y) =>
                                    recv.Use(x.Mikealphagolfnamekilojulietjulietkilocharliehotelgolf)
                                        .Merge(limamikeindiaSymbolmik!, papaNovembergolfNameind.Value)
                                );
                            }
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Wide links: the arguments' line stops at 113, and is null breaks behind a short lead (SK-DIV-0760).
    /// </summary>
    [Fact]
    public void ALoneCallWithWideLinks_BreaksTheArrowWhereverItsArgumentsEnd() =>
        Oracle.Agrees(
            """
            class C {
                object M610() {
                    if (c0) { _ = Enumerate(_ => Head(indiaSymbolJulietalphaBravochar, julietalphaBravocharlieDeltaech, alphaBravocharlieDeltaechoValueF).Select(z => z.Value).ToArray().Value); }
                }
                object M614() {
                    if (c0) { _ = Enumerate(_ => Head(charlieDeltaechoValueFoxtrotgolf, deltaechoValueFoxtrotgolfNameHot, echoValueFoxtrotgolfNameHotelindia).Select(z => z.Value).ToArray().Value); }
                }
                object M1455() {
                    Ussssssssssssssssssssssssssssssssssssssssss(x => recv.Use(charlieDeltaechoValueFoxtr, deltaechoValueFoxtrotgolfN) is null);
                }
                object M1568() {
                    Usssssssssssssssssssssssssssssssssssssssssss(x => recv.Use(deltaechoValueFoxtrotgolfN, echoValueFoxtrotgolfNameHo) is null);
                }
            }
            """,
            """
            class C {
                object M610() {
                    if (c0) {
                        _ = Enumerate(_ =>
                            Head(indiaSymbolJulietalphaBravochar, julietalphaBravocharlieDeltaech, alphaBravocharlieDeltaechoValueF)
                                .Select(z => z.Value)
                                .ToArray()
                                .Value
                        );
                    }
                }

                object M614() {
                    if (c0) {
                        _ = Enumerate(_ =>
                            Head(
                                    charlieDeltaechoValueFoxtrotgolf,
                                    deltaechoValueFoxtrotgolfNameHot,
                                    echoValueFoxtrotgolfNameHotelindia
                                )
                                .Select(z => z.Value)
                                .ToArray()
                                .Value
                        );
                    }
                }

                object M1455() {
                    Ussssssssssssssssssssssssssssssssssssssssss(x =>
                        recv.Use(charlieDeltaechoValueFoxtr, deltaechoValueFoxtrotgolfN) is null
                    );
                }

                object M1568() {
                    Usssssssssssssssssssssssssssssssssssssssssss(x =>
                        recv.Use(deltaechoValueFoxtrotgolfN, echoValueFoxtrotgolfNameHo) is null
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A one-line type test over a call of two arguments or more that fits beside the arrow, the test not, breaks the
    ///     arrow (SK-DIV-0984).
    /// </summary>
    [Fact]
    public void ATypeTestOverACallThatFitsBesideTheArrow_BreaksIt() =>
        Oracle.Agrees(
            """
            class C {
                object M719() {
                    TryFind(x => Analyze(x.Mikecharlieindiasymb, novemberalphaLimacharlieBrav, golfNameNovemberoscaros.Value) is IMethodSymbol { IsStatic: true });
                }
                object M251() {
                    if (c0) { if (c1) { r = Each((x, y) => model.Find(julietalphaNovem, mikeHotelcharli.Value, novemberPapaoscarBravogol!) is IMethodSymbol { IsStatic: true }); } }
                }
                object M2474() {
                    Use((x, y) => recv.Use(papaJulietPapaechoVal, GetRoot(kilooscarcharlieindiaSy), alphaLimaPapacharliekiloF!) is IMethodSymbol { IsStatic: true });
                }
            }
            """,
            """
            class C {
                object M719() {
                    TryFind(x =>
                        Analyze(
                            x.Mikecharlieindiasymb,
                            novemberalphaLimacharlieBrav,
                            golfNameNovemberoscaros.Value
                        ) is IMethodSymbol { IsStatic: true }
                    );
                }

                object M251() {
                    if (c0) {
                        if (c1) {
                            r = Each((x, y) =>
                                model.Find(julietalphaNovem, mikeHotelcharli.Value, novemberPapaoscarBravogol!) is IMethodSymbol {
                                    IsStatic: true
                                }
                            );
                        }
                    }
                }

                object M2474() {
                    Use((x, y) =>
                        recv.Use(
                            papaJulietPapaechoVal,
                            GetRoot(kilooscarcharlieindiaSy),
                            alphaLimaPapacharliekiloF!
                        ) is IMethodSymbol { IsStatic: true }
                    );
                }
            }
            """
        );

    /// <summary>
    ///     An arrow ending at 60 to 74 is the measured lines', the thousandth line with its far-right term (SK-DIV-0980).
    /// </summary>
    [Fact]
    public void AnArrowEndingFarRight_IsTheMeasuredLinesWithTheFarRightTerm() =>
        Oracle.Agrees(
            """
            class C {
                object M1862() {
                    if (c0) { var symbols = context.Compilation.Use(static x => base.Head(x.Novemberdeltagolfnamegolfnamefo, novemberoscarkiloalphaNove, indiaSymbolcharlieHo) is SyntaxNode node); }
                }
                object M548() {
                    if (c0) { var symbols = context.Compilation.Use(static x => model.Normalize(hotelmikeHotelkiloBravomikeBravoNovember!, "papacharlieFoxtrotechoValueJulietDeltago") is SyntaxNode node); }
                }
                object M832() {
                    var symbols = context.Compilation.Use(static (x, y) => this.CharlieHotelalphaBravoJuliet(echoValuekiloLimakilocharlieindi, oscarmikeDeltaNovemberNovembe));
                }
                object M833() {
                    var symbols = context.Compilation.Use(static (x, y) => model.OscarBravoFoxtrotDeltaoscar(hotelalphakiloechoValueNovember, foxtrotHotelLimamikeJulietNovem));
                }
                object M834() {
                    var symbols = context.Compilation.Use(static (x, y) => model.HoteloscarindiaSymbolFoxtro(bravokilogolfNamecharlieJulietin, echoValueJulietNovemberLimaPapa));
                }
            }
            """,
            """
            class C {
                object M1862() {
                    if (c0) {
                        var symbols = context.Compilation.Use(static x => base.Head(
                                x.Novemberdeltagolfnamegolfnamefo,
                                novemberoscarkiloalphaNove,
                                indiaSymbolcharlieHo
                            ) is SyntaxNode node
                        );
                    }
                }

                object M548() {
                    if (c0) {
                        var symbols = context.Compilation.Use(static x => model.Normalize(
                                hotelmikeHotelkiloBravomikeBravoNovember!,
                                "papacharlieFoxtrotechoValueJulietDeltago"
                            ) is SyntaxNode node
                        );
                    }
                }

                object M832() {
                    var symbols = context.Compilation.Use(static (x, y) =>
                        this.CharlieHotelalphaBravoJuliet(echoValuekiloLimakilocharlieindi, oscarmikeDeltaNovemberNovembe)
                    );
                }

                object M833() {
                    var symbols = context.Compilation.Use(static (x, y) =>
                        model.OscarBravoFoxtrotDeltaoscar(hotelalphakiloechoValueNovember, foxtrotHotelLimamikeJulietNovem)
                    );
                }

                object M834() {
                    var symbols = context.Compilation.Use(static (x, y) => model.HoteloscarindiaSymbolFoxtro(
                            bravokilogolfNamecharlieJulietin,
                            echoValueJulietNovemberLimaPapa
                        )
                    );
                }
            }
            """
        );

    /// <summary>
    ///     On the thousandth line's own boundary the arrow stays at columns 30 to 34 and 50 to 54 and breaks between
    ///     (SK-DIV-0981).
    /// </summary>
    [Fact]
    public void TheLinesOwnBoundary_BreaksTheTieByTheArrowsColumn() =>
        Oracle.Agrees(
            """
            class C {
                object M269() {
                    if (c0) { r = Each((x, y) => this.LookupTheDeclaration(limamikeNovemberPapaechoValuealphaindiaSym, kiloFoxtrotmikeJulietHotelmikecharlieLi) is IMethodSymbol { IsStatic: true }); }
                }
                object M106() {
                    Usesssssssss((a, b) => DeltaFoxtrotalphaindiaSym(hotelechoValueDeltaLimakiloLimaoscarDe, charlieNovemberHotelalphakiloDeltaFoxtr) is INamedTypeSymbol { });
                }
                object M286() {
                    Usesssssssssssssssssss((a, b) => this.cache.EchoValueindia(charlieFoxtrotgolfNameBravoHotelBravo, oscaralphaoscargolfNameDeltaPapaoscarDel) is INamedTypeSymbol { });
                }
            }
            """,
            """
            class C {
                object M269() {
                    if (c0) {
                        r = Each((x, y) => this.LookupTheDeclaration(
                                limamikeNovemberPapaechoValuealphaindiaSym,
                                kiloFoxtrotmikeJulietHotelmikecharlieLi
                            ) is IMethodSymbol { IsStatic: true }
                        );
                    }
                }

                object M106() {
                    Usesssssssss((a, b) => DeltaFoxtrotalphaindiaSym(
                            hotelechoValueDeltaLimakiloLimaoscarDe,
                            charlieNovemberHotelalphakiloDeltaFoxtr
                        ) is INamedTypeSymbol { }
                    );
                }

                object M286() {
                    Usesssssssssssssssssss((a, b) =>
                        this.cache.EchoValueindia(
                            charlieFoxtrotgolfNameBravoHotelBravo,
                            oscaralphaoscargolfNameDeltaPapaoscarDel
                        ) is INamedTypeSymbol { }
                    );
                }
            }
            """
        );

    /// <summary>
    ///     With links the short-lead region is 11·λ + 10·h + 30·L ≤ 370 (SK-DIV-0981).
    /// </summary>
    [Fact]
    public void AShortLeadWithLinks_WeighsTheLambdaATenthMore() =>
        Oracle.Agrees(
            """
            class C {
                object M362() {
                    var symbols = context.Compilation.Use(static x => recv.Nove(golfNameFoxtrotoscarindiaSymbolgolf, julietLimaindiaSymbolechoValueHote).Name);
                }
                object M363() {
                    var symbols = context.Compilation.Use(static x => Bravokilo(bravoFoxtrotJulietalphacharliem, foxtrotJulietmikeBravoindiaSymbolBravoP).Name);
                }
                object M364() {
                    var symbols = context.Compilation.Use(static x => GolfNamec(charliemikeoscaroscaralphaalphaNov, echoValuemikeoscarPapaFoxtrotechoValu).Name);
                }
                object M335() {
                    var symbols = context.Compilation.Use((a, b) => Limamikegol(hotelHotelFoxtrotNovemberechoVal, julietalphaLimaindiaSymbolcharliegolf).Name);
                }
                object M336() {
                    var symbols = context.Compilation.Use((a, b) => recv.Kilomi(limaNovemberDeltaBravokilooscarFoxt, foxtrotJulietalphaalphakilokiloDelt).Name);
                }
            }
            """,
            """
            class C {
                object M362() {
                    var symbols = context.Compilation.Use(static x =>
                        recv.Nove(golfNameFoxtrotoscarindiaSymbolgolf, julietLimaindiaSymbolechoValueHote).Name
                    );
                }

                object M363() {
                    var symbols = context.Compilation.Use(static x =>
                        Bravokilo(bravoFoxtrotJulietalphacharliem, foxtrotJulietmikeBravoindiaSymbolBravoP).Name
                    );
                }

                object M364() {
                    var symbols = context.Compilation.Use(static x => GolfNamec(
                            charliemikeoscaroscaralphaalphaNov,
                            echoValuemikeoscarPapaFoxtrotechoValu
                        )
                        .Name
                    );
                }

                object M335() {
                    var symbols = context.Compilation.Use((a, b) => Limamikegol(
                            hotelHotelFoxtrotNovemberechoVal,
                            julietalphaLimaindiaSymbolcharliegolf
                        )
                        .Name
                    );
                }

                object M336() {
                    var symbols = context.Compilation.Use((a, b) => recv.Kilomi(
                            limaNovemberDeltaBravokilooscarFoxt,
                            foxtrotJulietalphaalphakilokiloDelt
                        )
                        .Name
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A lone call whose line is over only by the ; after the outer ) chops by where the lambda starts and its lead
    ///     (SK-DIV-0982).
    /// </summary>
    [Fact]
    public void ALoneCallOverOnlyItsSemicolon_ChopsOrMovesTheParenthesisDown() =>
        Oracle.Agrees(
            """
            class C {
                object M297() {
                    if (c0) { return items.Select(static x => model.Head(charliePapaPap.Value, (int)alphaPapamikeLima, (int)limaPapaindi)); }
                }
                object M333() {
                    _ = Enumerate(_ => this.GetTheSymbolOf(GetRoot(limaJulietNovembermik), alphaFoxtrotgolfNameNovemberoscarmikePa));
                }
                object M669() {
                    Usessssss((a, b) => this.cache.Alphacharlie(alphagolfNameJulietalphaJulietL, mikeLimaoscarLimaDeltagolfNamePap));
                }
                object M670() {
                    Usesssssss((a, b) => model.AlphaoscarPapaPap(kiloNovemberindiaSymbolNovemberalp, papaoscarPapamikegolfNameBrav));
                }
            }
            """,
            """
            class C {
                object M297() {
                    if (c0) {
                        return items.Select(static x => model.Head(
                                charliePapaPap.Value,
                                (int)alphaPapamikeLima,
                                (int)limaPapaindi
                            )
                        );
                    }
                }

                object M333() {
                    _ = Enumerate(_ => this.GetTheSymbolOf(
                            GetRoot(limaJulietNovembermik),
                            alphaFoxtrotgolfNameNovemberoscarmikePa
                        )
                    );
                }

                object M669() {
                    Usessssss((a, b) => this.cache.Alphacharlie(
                            alphagolfNameJulietalphaJulietL,
                            mikeLimaoscarLimaDeltagolfNamePap
                        )
                    );
                }

                object M670() {
                    Usesssssss((a, b) => model.AlphaoscarPapaPap(kiloNovemberindiaSymbolNovemberalp, papaoscarPapamikegolfNameBrav)
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A chain whose first call does not fit beside the arrow behind a receiver of 12 columns or more is decided as a
    ///     chain of properties (SK-DIV-0983).
    /// </summary>
    [Fact]
    public void AChainBehindALongReceiver_IsDecidedAsAChainOfProperties() =>
        Oracle.Agrees(
            """
            class C {
                object M589() {
                    if (c0) { _ = Enumerate(static x => novemberJulietLimaalphaindiaSymbolmikeLimaFoxtrotoscarLimaFoxtrotP.ComputeTheValueFor().Name.ToString()); }
                }
                object M1165() {
                    if (c0) { if (c1) { if (c2) { Use(node => limaLimaFoxtrotBravoHotelJulietkiloFoxtrotcharliePapacharliekiloechoValuee.model.Resolve().First().Name.Kind); } } }
                }
                object M22() {
                    if (c0) { this.Apply((a, b) => oscarJulietLimaechoValueHotelNovemberFoxtrotgolfNamemikeFoxtrotechoValueLimaFox.Open(a, b).Count); }
                }
                object M97() {
                    r = Each(static (x, y) => indiaSymbolgolfNamecharlieindiaSymbolPapacharliecharlieoscarNovemberDeltaJulietoscarmi.Open(a, b).Where(y => y.IsValid).ToArray().Where(y => y.IsValid).ToArray());
                }
            }
            """,
            """
            class C {
                object M589() {
                    if (c0) {
                        _ = Enumerate(static x => novemberJulietLimaalphaindiaSymbolmikeLimaFoxtrotoscarLimaFoxtrotP
                            .ComputeTheValueFor()
                            .Name.ToString()
                        );
                    }
                }

                object M1165() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                Use(node => limaLimaFoxtrotBravoHotelJulietkiloFoxtrotcharliePapacharliekiloechoValuee.model
                                    .Resolve()
                                    .First()
                                    .Name.Kind
                                );
                            }
                        }
                    }
                }

                object M22() {
                    if (c0) {
                        this.Apply((a, b) => oscarJulietLimaechoValueHotelNovemberFoxtrotgolfNamemikeFoxtrotechoValueLimaFox
                            .Open(a, b)
                            .Count
                        );
                    }
                }

                object M97() {
                    r = Each(static (x, y) => indiaSymbolgolfNamecharlieindiaSymbolPapacharliecharlieoscarNovemberDeltaJulietoscarmi
                        .Open(a, b)
                        .Where(y => y.IsValid)
                        .ToArray()
                        .Where(y => y.IsValid)
                        .ToArray()
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A chain of properties that fits nowhere breaks its arrow by 102·s − 180·p − 13·f ≥ 716 (SK-DIV-0985).
    /// </summary>
    [Fact]
    public void APropertyChainThatFitsNowhere_ByTheRefittedLine() =>
        Oracle.Agrees(
            """
            class C {
                object M798() {
                    if (c0) { if (c1) { this.Apply(symbol => symbol.Novembe.Mikejulietos.Alphakil.Julietnovemberg.Novem.Limafo.Charlie.Mikenovemberind.Hote.Echovaluedeltan.Papaindiasy.Novem.Charlieindiasymbo.Alphaf.Hotelno.Kilomike); } }
                }
                object M1329() {
                    if (c0) { if (c1) { if (c2) { var r = recv.Use(static (x, y) => this.model.Oscarindiasymbol.Julietm.Novembergolfna.Echo.Indiasymbolosc.Kilo.Deltahotelgolfname.Bravocharliepapam.Foxtrotbravoechova); } } }
                }
            }
            """,
            """
            class C {
                object M798() {
                    if (c0) {
                        if (c1) {
                            this.Apply(symbol =>
                                symbol.Novembe.Mikejulietos.Alphakil.Julietnovemberg.Novem.Limafo.Charlie.Mikenovemberind.Hote
                                    .Echovaluedeltan.Papaindiasy.Novem.Charlieindiasymbo.Alphaf.Hotelno.Kilomike
                            );
                        }
                    }
                }

                object M1329() {
                    if (c0) {
                        if (c1) {
                            if (c2) {
                                var r = recv.Use(static (x, y) =>
                                    this.model.Oscarindiasymbol.Julietm.Novembergolfna.Echo.Indiasymbolosc.Kilo.Deltahotelgolfname
                                        .Bravocharliepapam.Foxtrotbravoechova
                                );
                            }
                        }
                    }
                }
            }
            """
        );
}
