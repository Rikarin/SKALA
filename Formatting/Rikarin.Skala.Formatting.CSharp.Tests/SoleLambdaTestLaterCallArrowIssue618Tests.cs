namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #618, SK-DIV-0520 round two: a sole lambda over a type test the author broke before its keyword, whose
///     operand ends in a later call of two or more arguments — <c>open(x).Analyze(a, b)</c>, a no-argument first call,
///     links between the calls or after the last, a first call of two or more — breaks its arrow by
///     <c>GroupFacts.LambdaTestLaterCall</c>'s line, and keeps it below that line by the operand's other rule. Every
///     expected string is <c>jb cleanupcode</c> 2025.2.6's own output, from probe I (seed 61944), written after the
///     rule.
/// </summary>
public sealed class SoleLambdaTestLaterCallArrowIssue618Tests {
    [Fact]
    public void ALaterCallOfTwoOrMore_BreaksTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                async void M168() {
                    await q.Run(static x => Lookup(JulietPapaDe).Select(JulietDeltamikeNodemikeNodeHotelHotelgolfNam, oscarechoValue, Deltaech)
            is SyntaxNode);
                }
                object M211() {
                    items.Use(_ => GetRoot().WithOptions(golfNameJulietindiaSymbolcharlieJulie, FoxtrotmikeNodeoscaralpha, alphaalph)
            as INamedTypeSymbol);
                }
                async void M1122() {
                    await q.Do(static x => Analyze(kiloDeltaBravoLi).First().Merge(NovembercharliemikeNodekilocharli, indiaSymboloscarDeltaoscarechoV)
            as INamedTypeSymbol);
                }
                object M87() {
                    if (c0) { _ = this.Do(static (x, y) => ctx.Resolve(DeltaLimaJulietFoxtrotc).Select(D, FoxtrotDeltaJulietechoValuealphaFox).Name.Length
            as INamedTypeSymbol); }
                }
                object M4() {
                    var found = list.Do(static x => Open(echoValueBravoechoV, charlieN).Select(JulietgolfNameHotelgolfNamePa, alphaBravoFoxtrotNovemberkiloFoxtrotoscarFoxt)
            as INamedTypeSymbol);
                }
            }
            """,
            """
            class C {
                async void M168() {
                    await q.Run(static x =>
                        Lookup(JulietPapaDe).Select(JulietDeltamikeNodemikeNodeHotelHotelgolfNam, oscarechoValue, Deltaech)
                            is SyntaxNode
                    );
                }

                object M211() {
                    items.Use(_ =>
                        GetRoot().WithOptions(golfNameJulietindiaSymbolcharlieJulie, FoxtrotmikeNodeoscaralpha, alphaalph)
                            as INamedTypeSymbol
                    );
                }

                async void M1122() {
                    await q.Do(static x =>
                        Analyze(kiloDeltaBravoLi).First().Merge(NovembercharliemikeNodekilocharli, indiaSymboloscarDeltaoscarechoV)
                            as INamedTypeSymbol
                    );
                }

                object M87() {
                    if (c0) {
                        _ = this.Do(static (x, y) =>
                            ctx.Resolve(DeltaLimaJulietFoxtrotc).Select(D, FoxtrotDeltaJulietechoValuealphaFox).Name.Length
                                as INamedTypeSymbol
                        );
                    }
                }

                object M4() {
                    var found = list.Do(static x =>
                        Open(echoValueBravoechoV, charlieN)
                                .Select(JulietgolfNameHotelgolfNamePa, alphaBravoFoxtrotNovemberkiloFoxtrotoscarFoxt)
                            as INamedTypeSymbol
                    );
                }
            }
            """
        );

    /// <summary>
    ///     Under the broken arrow the operand's links stack two levels past the operand's line and the test one, as
    ///     for every other operand on a call's spine (#602).
    /// </summary>
    [Fact]
    public void UnderTheBrokenArrow_TheLinksStack() =>
        Oracle.Agrees(
            """
            class C {
                async void M0() {
                    if (c0) { if (c1) { await q.Do((x) => Lookup(echoValueNovemberFoxtrotcharliealphaJulietFoxtrotJulietgolf).Select(kiloDelt, charlie, HotelBravooscarJulietDeltaF)
            is not null); } }
                }
                object M6() {
                    items.Do(x => Lookup(kiloJulietgolfNamecharlie).WithOptions(indiaSymbolalphacharlieFoxtrotDeltaa, charlie, alphaPapaPapaFoxtro, golfNameD).Result
            as INamedTypeSymbol);
                }
            }
            """,
            """
            class C {
                async void M0() {
                    if (c0) {
                        if (c1) {
                            await q.Do((x) =>
                                Lookup(echoValueNovemberFoxtrotcharliealphaJulietFoxtrotJulietgolf)
                                        .Select(kiloDelt, charlie, HotelBravooscarJulietDeltaF)
                                    is not null
                            );
                        }
                    }
                }

                object M6() {
                    items.Do(x =>
                        Lookup(kiloJulietgolfNamecharlie)
                                .WithOptions(indiaSymbolalphacharlieFoxtrotDeltaa, charlie, alphaPapaPapaFoxtro, golfNameD)
                                .Result
                            as INamedTypeSymbol
                    );
                }
            }
            """
        );

    /// <summary>
    ///     A narrow callee in front of a long parameter list keeps the arrow, and the operand's links stack two levels
    ///     past the line, the test one.
    /// </summary>
    [Fact]
    public void BelowTheLine_KeepsTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M1097() {
                    if (c0) { if (c1) { X((Node node) => this.cache.Read(NovemberDeltaPapaBravoFoxtrotindiaSymbole).Merge(indiaSymbolPap, DeltaDeltaalphamikeNodePap)
            is { IsStatic: true }); } }
                }
                object M202() {
                    X((Node node) => Open(indiaSymbolcharliecharliegolfNameLimaFoxtrotLimagolfNa).WithOptions(oscargolfNameJulietechoValue, kiloos).Result
            as INamedTypeSymbol);
                }
            }
            """,
            """
            class C {
                object M1097() {
                    if (c0) {
                        if (c1) {
                            X((Node node) => this.cache.Read(NovemberDeltaPapaBravoFoxtrotindiaSymbole)
                                    .Merge(indiaSymbolPap, DeltaDeltaalphamikeNodePap)
                                is { IsStatic: true }
                            );
                        }
                    }
                }

                object M202() {
                    X((Node node) => Open(indiaSymbolcharliecharliegolfNameLimaFoxtrotLimagolfNa)
                            .WithOptions(oscargolfNameJulietechoValue, kiloos)
                            .Result
                        as INamedTypeSymbol
                    );
                }
            }
            """
        );
}
