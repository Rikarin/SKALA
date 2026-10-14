namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #620, SK-DIV-0560: a sole lambda whose whole body is a one-argument call with links after it breaks its
///     arrow when the call fits on the line below and not beside the arrow. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output, from probe J (seed 560201), written after the rule was chosen.
/// </summary>
public sealed class SoleLambdaLinkedCallArrowIssue620Tests {
    [Fact]
    public void ACallThatFitsOnlyBelow_BreaksTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M29() {
                    _ = Enumerate((Node node) => this.cache.Read((int)indiaSymbolJulietechoValueechoValueDeltaindiaSymbolindiaSymbolDeltaind).First().Name);
                }
                object M126() {
                    bool ok = nodes.All(x => LookupTheDeclaration(indiaSymbolindiaSymbolgolfNameDeltacharliealphaDeltaHotelalphagolfNameFoxtrot!).Name.Length);
                }
                object M370() {
                    if (c0) { if (c1) { bool ok = this.Apply(static x => Bind(GetRoot(golfNamegolfNameindiaSymbolalphaHotelBravoHotelcharlieechoValueindiaSymbolBrav)).GetHashCode()); } }
                }
            }
            """,
            """
            class C {
                object M29() {
                    _ = Enumerate((Node node) =>
                        this.cache.Read((int)indiaSymbolJulietechoValueechoValueDeltaindiaSymbolindiaSymbolDeltaind).First().Name
                    );
                }

                object M126() {
                    bool ok = nodes.All(x =>
                        LookupTheDeclaration(indiaSymbolindiaSymbolgolfNameDeltacharliealphaDeltaHotelalphagolfNameFoxtrot!)
                            .Name.Length
                    );
                }

                object M370() {
                    if (c0) {
                        if (c1) {
                            bool ok = this.Apply(static x =>
                                Bind(GetRoot(golfNamegolfNameindiaSymbolalphaHotelBravoHotelcharlieechoValueindiaSymbolBrav))
                                    .GetHashCode()
                            );
                        }
                    }
                }
            }
            """
        );

    /// <summary>A call that fits beside the arrow keeps it, and the links break below.</summary>
    [Fact]
    public void ACallThatFitsBeside_KeepsTheArrow() =>
        Oracle.Agrees(
            """
            class C {
                object M47() {
                    this.Apply(_ => Bind(DeltaHotelcharlieDeltaJulietgolfNameindiaSymbolFoxtrotindiaSymbolgolfNameHotelJulietJ).Where(y => y.IsValid));
                }
                object M61() {
                    r = Each((x) => this.cache.Read(echoValuegolfNamegolfNamegolfNameindiaSymbolJulietalphacharlie.Value).Select(z => z.Name));
                }
            }
            """,
            """
            class C {
                object M47() {
                    this.Apply(_ => Bind(DeltaHotelcharlieDeltaJulietgolfNameindiaSymbolFoxtrotindiaSymbolgolfNameHotelJulietJ)
                        .Where(y => y.IsValid)
                    );
                }

                object M61() {
                    r = Each((x) => this.cache.Read(echoValuegolfNamegolfNamegolfNameindiaSymbolJulietalphacharlie.Value)
                        .Select(z => z.Name)
                    );
                }
            }
            """
        );
}
