using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issues #501, #502 and #503, SK-DIV-0198: what the oracle measures before breaking at the <c>:</c> of a type with a
///     parameter list, and where the base types land once it has. Every expected string is <c>jb cleanupcode</c>
///     2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class PrimaryConstructorBaseListIssue501Tests {
    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    /// <summary>
    ///     #501: interfaces after a primary constructor&apos;s base type. The questions end at the first comma, and the
    ///     argument list is no place to break; the line through the comma stays when it fits, and moves below when it fits
    ///     there.
    /// </summary>
    [Fact]
    public void InterfacesAfterTheBaseType_BreakBeforeTheColonWhenTheLineThroughTheCommaFitsBelow() =>
        Agrees(
            """
            class O1 {
                class O2 {
                    class M3(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betaValueArgubaaaaaaaaaaaaaaaaaaaaaaa), IFirstInterface, ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M83(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M84(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M100(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M110(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbb { }
                }
            }

            class O1 {
                class O2 {
                    class V8(int alphaValue, int betaValue) : BaseTypeName(alphaValue, betaValue), IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXX { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class M3(int alphaValue, int betaValue)
                        : BaseTypeName(alphaValueArgument, betaValueArgubaaaaaaaaaaaaaaaaaaaaaaa),
                            IFirstInterface,
                            ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M83(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbb),
                        IFirstInterface,
                        ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M84(int alphaValue, int betaValue)
                        : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb),
                            IFirstInterface,
                            ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M100(int alphaValue, int betaValue)
                        : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb),
                            IFirstInterface,
                            ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M110(int alphaValue, int betaValue) : BaseTypeName(
                            alphaValueArgument,
                            betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                        ),
                        IFirstInterface,
                        ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                            aaaaaaaaaaaaaaaaaaaaaa,
                            bbbbbbbbbbbbbbbbbbbbbbbbbb,
                            cccccccccccccccccccccccccccccc,
                            ddddddddddddddddddddddddddd
                        ),
                        IAaaaa,
                        IBbbbb { }
                }
            }

            class O1 {
                class O2 {
                    class V8(int alphaValue, int betaValue) : BaseTypeName(alphaValue, betaValue),
                        IFirstInterfaceWithALongName,
                        ISecondInterfaceWithALongNameXX { }
                }
            }
            """
        );

    /// <summary>
    ///     Any type with a parameter list: the base type without arguments, generic, an interface list, a record. The
    ///     break goes before the colon when the list then fits below.
    /// </summary>
    [Fact]
    public void AnyPrimaryConstructorType_ItsColonIsTheInitializerPoint() =>
        Agrees(
            """
            class O1 {
                class O2 {
                    class X1(int alphaValue, int betaValue, int gammaValue) : BaseTypeNameThatIsVeryLongIndeedXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X2(int alphaValue, int betaValue) : IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X5(int alphaValue, int betaValue, int gammaValue, int deltaValue, int epsilonValue) : BaseTypeNameLonger, IFirstInterfaceWithALongName { }
                }
            }

            class O1 {
                class O2 {
                    record X8(int AlphaValue, int BetaValue) : BaseRecord, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXX;
                }
            }

            class O1 {
                class O2 {
                    class X9(int alphaValue, int betaValue) : BaseTypeName<int>, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXX { }
                }
            }

            class O1 {
                class O2 {
                    class V2(int alphaValue, int betaValue) : BaseTypeName, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X4(int alphaValue, int betaValue) : BaseTypeName, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXXXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X7() : BaseTypeName, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXXXXXXXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class X1(int alphaValue, int betaValue, int gammaValue)
                        : BaseTypeNameThatIsVeryLongIndeedXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X2(int alphaValue, int betaValue)
                        : IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X5(int alphaValue, int betaValue, int gammaValue, int deltaValue, int epsilonValue)
                        : BaseTypeNameLonger, IFirstInterfaceWithALongName { }
                }
            }

            class O1 {
                class O2 {
                    record X8(int AlphaValue, int BetaValue)
                        : BaseRecord, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXX;
                }
            }

            class O1 {
                class O2 {
                    class X9(int alphaValue, int betaValue)
                        : BaseTypeName<int>, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXX { }
                }
            }

            class O1 {
                class O2 {
                    class V2(int alphaValue, int betaValue)
                        : BaseTypeName, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X4(int alphaValue, int betaValue) : BaseTypeName,
                        IFirstInterfaceWithALongName,
                        ISecondInterfaceWithALongNameXXXXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X7() : BaseTypeName,
                        IFirstInterfaceWithALongName,
                        ISecondInterfaceWithALongNameXXXXXXXXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }
            """
        );

    /// <summary>
    ///     #502: at skala_wrap_before_extends_colon = true the oracle keeps : B( and chops a list that would fit below,
    ///     breaks before the colon when the head overflows, and asks the interfaces&apos; question as at false.
    /// </summary>
    [Fact]
    public void WrapBeforeExtendsColon_KeepsTheHeadAndChops() =>
        Agrees(
            """
            class O1 {
                class O2 {
                    class M4(int alphaValue, int betaValue) : BaseTypeName(alphaValue, betaValuebaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa) { }
                }
            }

            class O1 {
                class O2 {
                    class S116(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) { }
                }
            }

            class O1 {
                class O2 {
                    class W1(int alphaValue, int betaValue, int gammaValue, int deltaValue, int epsilonValue, int zetaV) : B(alphaValue, betaValue) { }
                }
            }

            class O1 {
                class O2 {
                    class V5(int alphaValue, int betaValue, int gammaValue, int deltaValue, int epsilonValue, int zeta) : BaseTypeName(alphaValue, betaValue) { }
                }
            }

            class O1 {
                class O2 {
                    class X1(int alphaValue, int betaValue, int gammaValue) : BaseTypeNameThatIsVeryLongIndeedXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X2(int alphaValue, int betaValue) : IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X4(int alphaValue, int betaValue) : BaseTypeName, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXXXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X5(int alphaValue, int betaValue, int gammaValue, int deltaValue, int epsilonValue) : BaseTypeNameLonger, IFirstInterfaceWithALongName { }
                }
            }

            class O1 {
                class O2 {
                    class X7() : BaseTypeName, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXXXXXXXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    record X8(int AlphaValue, int BetaValue) : BaseRecord, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXX;
                }
            }

            class O1 {
                class O2 {
                    class M3(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betaValueArgubaaaaaaaaaaaaaaaaaaaaaaa), IFirstInterface, ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M116(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class V8(int alphaValue, int betaValue) : BaseTypeName(alphaValue, betaValue), IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXX { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class M4(int alphaValue, int betaValue) : BaseTypeName(
                        alphaValue,
                        betaValuebaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                    ) { }
                }
            }

            class O1 {
                class O2 {
                    class S116(int alphaValue, int betaValue) : BaseTypeName(
                        alphaValueArgument,
                        betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                    ) { }
                }
            }

            class O1 {
                class O2 {
                    class W1(int alphaValue, int betaValue, int gammaValue, int deltaValue, int epsilonValue, int zetaV) : B(
                        alphaValue,
                        betaValue
                    ) { }
                }
            }

            class O1 {
                class O2 {
                    class V5(int alphaValue, int betaValue, int gammaValue, int deltaValue, int epsilonValue, int zeta)
                        : BaseTypeName(alphaValue, betaValue) { }
                }
            }

            class O1 {
                class O2 {
                    class X1(int alphaValue, int betaValue, int gammaValue)
                        : BaseTypeNameThatIsVeryLongIndeedXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X2(int alphaValue, int betaValue) : IFirstInterfaceWithALongName,
                        ISecondInterfaceWithALongNameXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X4(int alphaValue, int betaValue) : BaseTypeName,
                        IFirstInterfaceWithALongName,
                        ISecondInterfaceWithALongNameXXXXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X5(int alphaValue, int betaValue, int gammaValue, int deltaValue, int epsilonValue) : BaseTypeNameLonger,
                        IFirstInterfaceWithALongName { }
                }
            }

            class O1 {
                class O2 {
                    class X7() : BaseTypeName,
                        IFirstInterfaceWithALongName,
                        ISecondInterfaceWithALongNameXXXXXXXXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    record X8(int AlphaValue, int BetaValue) : BaseRecord,
                        IFirstInterfaceWithALongName,
                        ISecondInterfaceWithALongNameXXX;
                }
            }

            class O1 {
                class O2 {
                    class M3(int alphaValue, int betaValue)
                        : BaseTypeName(alphaValueArgument, betaValueArgubaaaaaaaaaaaaaaaaaaaaaaa),
                            IFirstInterface,
                            ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M116(int alphaValue, int betaValue) : BaseTypeName(
                            alphaValueArgument,
                            betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                        ),
                        IFirstInterface,
                        ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class V8(int alphaValue, int betaValue) : BaseTypeName(alphaValue, betaValue),
                        IFirstInterfaceWithALongName,
                        ISecondInterfaceWithALongNameXX { }
                }
            }
            """,
            ("skala_wrap_before_extends_colon", "true")
        );

    /// <summary>
    ///     #503: after a break before the colon the base types are one level past the colon&apos;s line, and a chopped
    ///     base type&apos;s arguments nest from there.
    /// </summary>
    [Fact]
    public void ColonOnItsOwnLine_TypesOneLevelPastIt() =>
        Agrees(
            """
            class O1 {
                class O2 {
                    class M116(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M121(int alphaValue, int betaValue) : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb), IFirstInterface, ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class P116(int alphaValue, int betaValue) : BaseTypeName(alphaValue, // e
                        betaValue), IFirstInterface, ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbb { }
                }
            }

            class O1 {
                class O2 {
                    class Bl4(int a, int b)
                        : B(a, // e
                            b), IFirst, ISecond { }
                }
            }

            class O1 {
                class O2 {
                    record Bl7(int a, int b) : B(a, // e
                        b), IFirst;
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class M116(int alphaValue, int betaValue)
                        : BaseTypeName(alphaValueArgument, betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb),
                            IFirstInterface,
                            ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M121(int alphaValue, int betaValue)
                        : BaseTypeName(
                                alphaValueArgument,
                                betabbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
                            ),
                            IFirstInterface,
                            ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class P116(int alphaValue, int betaValue)
                        : BaseTypeName(
                                alphaValue, // e
                                betaValue
                            ),
                            IFirstInterface,
                            ISecondInterface { }
                }
            }

            class O1 {
                class O2 {
                    class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb)
                        : Bbbbbbbbbbbbbbbbbbbbbbbb(
                                aaaaaaaaaaaaaaaaaaaaaa,
                                bbbbbbbbbbbbbbbbbbbbbbbbbb,
                                cccccccccccccccccccccccccccccc,
                                ddddddddddddddddddddddddddd
                            ),
                            IAaaaa,
                            IBbbbb { }
                }
            }

            class O1 {
                class O2 {
                    class Bl4(int a, int b)
                        : B(
                                a, // e
                                b
                            ),
                            IFirst,
                            ISecond { }
                }
            }

            class O1 {
                class O2 {
                    record Bl7(int a, int b)
                        : B(
                                a, // e
                                b
                            ),
                            IFirst;
                }
            }
            """,
            ("skala_place_primary_constructor_initializer_on_same_line", "false")
        );

    /// <summary>
    ///     #503 at the export: an author&apos;s break before the colon kept, and the fitter&apos;s, put the chopped types
    ///     one level past the colon&apos;s line.
    /// </summary>
    [Fact]
    public void KeptBreakBeforeTheColon_TypesOneLevelPastIt() =>
        Agrees(
            """
            class O1 {
                class O2 {
                    class Bl2
                        : IFirstInterfaceWithALongName, ISecondInterfaceWithALongName, IThirdInterfaceWithAnEvenLongerNameXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class Bl4(int a, int b)
                        : B(a, // e
                            b), IFirst, ISecond { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class Bl2
                        : IFirstInterfaceWithALongName,
                            ISecondInterfaceWithALongName,
                            IThirdInterfaceWithAnEvenLongerNameXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class Bl4(int a, int b)
                        : B(
                                a, // e
                                b
                            ),
                            IFirst,
                            ISecond { }
                }
            }
            """
        );

    /// <summary>#503 at skala_wrap_before_extends_colon = true on an ordinary base list.</summary>
    [Fact]
    public void WrapBeforeExtendsColon_TypesOneLevelPastTheColon() =>
        Agrees(
            """
            class O1 {
                class O2 {
                    class Bl5 : BaseClass<IFirstTypeArgumentWithALongName, ISecondTypeArgumentWithALongName>, IThirdInterfaceWithAnEvenLongerName { }
                }
            }

            class O1 {
                class O2 {
                    class Bl2
                        : IFirstInterfaceWithALongName, ISecondInterfaceWithALongName, IThirdInterfaceWithAnEvenLongerNameXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X6 : BaseTypeName, IFirstInterfaceWithALongName, ISecondInterfaceWithALongNameXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }
            """,
            """
            class O1 {
                class O2 {
                    class Bl5
                        : BaseClass<IFirstTypeArgumentWithALongName, ISecondTypeArgumentWithALongName>,
                            IThirdInterfaceWithAnEvenLongerName { }
                }
            }

            class O1 {
                class O2 {
                    class Bl2
                        : IFirstInterfaceWithALongName,
                            ISecondInterfaceWithALongName,
                            IThirdInterfaceWithAnEvenLongerNameXXXXXXXX { }
                }
            }

            class O1 {
                class O2 {
                    class X6
                        : BaseTypeName,
                            IFirstInterfaceWithALongName,
                            ISecondInterfaceWithALongNameXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX { }
                }
            }
            """,
            ("skala_wrap_before_extends_colon", "true")
        );
}
