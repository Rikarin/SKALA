using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #427, SK-DIV-0197: a primary constructor's base type with arguments is laid out as an
///     initializer. Its break goes before the <c>:</c> and only when the list then fits; otherwise
///     <c>: B(</c> stays on the declaration's line and the arguments chop. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input, under each
///     <c>skala_wrap_extends_list_style</c> value, and each test asserts the second pass too.
/// </summary>
public sealed class PrimaryConstructorBaseTypeIssue427Tests {
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
    ///     The issue's shape and its family: a class, a record and a struct whose base type's arguments are chopped by a
    ///     comment or an author's break keep <c>: B(</c> on the declaration's line; a list too long for the line breaks before
    ///     the <c>:</c> when it then fits, and keeps <c>: B(</c> and chops when it does not; interfaces after it and a
    ///     constraint, at two depths.
    /// </summary>
    [Fact]
    public void ChoppedArguments_KeepTheColonOnTheLine_ChopIfLong() =>
        Agrees(
            """
            class L(int a, int b) : B(a, b // e
            ) {
            }

            class L2(int a, int b) : B(a, b /*e*/
            ) {
            }

            record R(int a, int b) : B(a, b // e
            );

            struct S(int a, int b) : I(a, b // e
            ) {
            }

            class L3(int a, int b) : B(a, b // e
            ), I1, I2 {
            }

            class L4<T>(int a, int b) : B(a, b // e
            ) where T : class {
            }

            class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) {
            }

            class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd) {
            }

            class L8(int a) : B(a,
                b) {
            }

            namespace N {
                class L(int a, int b) : B(a, b // e
                ) {
                }

                class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd) {
                }

                record R(int a, int b) : B(a, b // e
                ), I1;
            }
            """,
            """
            class L(int a, int b) : B(
                a,
                b // e
            ) { }

            class L2(int a, int b) : B(
                a,
                b /*e*/
            ) { }

            record R(int a, int b) : B(
                a,
                b // e
            );

            struct S(int a, int b) : I(
                a,
                b // e
            ) { }

            class L3(int a, int b) : B(
                    a,
                    b // e
                ),
                I1,
                I2 { }

            class L4<T>(int a, int b) : B(
                a,
                b // e
            ) where T : class { }

            class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb)
                : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) { }

            class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                aaaaaaaaaaaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbbbbbbbbb,
                cccccccccccccccccccccccccccccc,
                ddddddddddddddddddddddddddd
            ) { }

            class L8(int a) : B(
                a,
                b
            ) { }

            namespace N {
                class L(int a, int b) : B(
                    a,
                    b // e
                ) { }

                class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                    aaaaaaaaaaaaaaaaaaaaaa,
                    bbbbbbbbbbbbbbbbbbbbbbbbbb,
                    cccccccccccccccccccccccccccccc,
                    ddddddddddddddddddddddddddd
                ) { }

                record R(int a, int b) : B(
                        a,
                        b // e
                    ),
                    I1;
            }
            """
        );

    /// <summary>
    ///     A head too long for <c>: B(</c> breaks before the colon; a chopped base type followed by interfaces nests its
    ///     arguments from the extends list's continuation line; a constructor initializer is unchanged; three depths.
    /// </summary>
    [Fact]
    public void HeadsInterfacesAndDepths_ChopIfLong() =>
        Agrees(
            """
            class M1VeryLongClassNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(int aaaaaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbb) {
            }

            class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbb {
            }

            class M3(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa), IAaaaaaaaaaaaaaaaaaaaaaaaaa, IBbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb {
            }

            class M4(
                int a,
                int b
            ) : B(a, b) {
            }

            class M5(int a, int b) : B(a, b // e
            ), I1 {
            }

            class C : X {
                C(int a, int b) : base(a, b // e
                ) {
                }

                C(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccc) : base(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) {
                }
            }

            namespace N {
                namespace O {
                    class L3(int a, int b) : B(a, b // e
                    ), I1, I2 {
                    }

                    class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbb {
                    }

                    class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) {
                    }
                }
            }
            """,
            """
            class M1VeryLongClassNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(int aaaaaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbb)
                : Bbbbbbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbb) { }

            class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                    aaaaaaaaaaaaaaaaaaaaaa,
                    bbbbbbbbbbbbbbbbbbbbbbbbbb,
                    cccccccccccccccccccccccccccccc,
                    ddddddddddddddddddddddddddd
                ),
                IAaaaa,
                IBbbbb { }

            class M3(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa),
                IAaaaaaaaaaaaaaaaaaaaaaaaaa,
                IBbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb { }

            class M4(
                int a,
                int b
            ) : B(a, b) { }

            class M5(int a, int b) : B(
                    a,
                    b // e
                ),
                I1 { }

            class C : X {
                C(int a, int b) : base(
                    a,
                    b // e
                ) { }

                C(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccc) : base(
                    aaaaaaaaaaaaaaaaaaaaaa,
                    bbbbbbbbbbbbbbbbbbbbbbbbbb
                ) { }
            }

            namespace N {
                namespace O {
                    class L3(int a, int b) : B(
                            a,
                            b // e
                        ),
                        I1,
                        I2 { }

                    class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                            aaaaaaaaaaaaaaaaaaaaaa,
                            bbbbbbbbbbbbbbbbbbbbbbbbbb,
                            cccccccccccccccccccccccccccccc,
                            ddddddddddddddddddddddddddd
                        ),
                        IAaaaa,
                        IBbbbb { }

                    class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb)
                        : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) { }
                }
            }
            """
        );

    /// <summary>
    ///     Generic base types, a comma inside the arguments, a <c>record struct</c>, an author's break before and after
    ///     the colon, and a lambda argument.
    /// </summary>
    [Fact]
    public void GenericAndAuthorBrokenShapes_ChopIfLong() =>
        Agrees(
            """
            class G : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, Cccccccccccccccccccccccccccccccc>, IBbbbbbbbbbbbbbbb {
            }

            class G2 : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>, IBbbbbbbbbbbbbbbb {
            }

            class P(int a) : B(a, // e
                a), I1, I2 {
            }

            record struct RS(int a, int b) : I(a, b // e
            ), I2;

            class X(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbbb {
            }

            class Y(int a, int b)
                : B(a, b) {
            }

            class Z(int a, int b) :
                B(a, b) {
            }

            class Q(int a, int b) : B(x => {
                return a;
            }) {
            }

            namespace N {
                class L(int a, int b) : B(a, b // e
                ), I1, I2 {
                }

                class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) {
                }
            }
            """,
            """
            class G : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
                    Cccccccccccccccccccccccccccccccc>,
                IBbbbbbbbbbbbbbbb { }

            class G2 : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                    Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>,
                IBbbbbbbbbbbbbbbb { }

            class P(int a) : B(
                    a, // e
                    a
                ),
                I1,
                I2 { }

            record struct RS(int a, int b) : I(
                    a,
                    b // e
                ),
                I2;

            class X(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                    aaaaaaaaaaaaaaaaaaaaaa,
                    bbbbbbbbbbbbbbbbbbbbbbbbbb,
                    cccccccccccccccccccccccccccccc,
                    ddddddddddddddddddddddddddd
                ),
                IAaaaa,
                IBbbbbb { }

            class Y(int a, int b)
                : B(a, b) { }

            class Z(int a, int b) :
                B(a, b) { }

            class Q(int a, int b) : B(x => { return a; }) { }

            namespace N {
                class L(int a, int b) : B(
                        a,
                        b // e
                    ),
                    I1,
                    I2 { }

                class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb)
                    : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) { }
            }
            """
        );

    /// <summary>A one-argument base type and a too-long argument at two depths.</summary>
    [Fact]
    public void OneArgumentAndTooLongArguments_ChopIfLong() =>
        Agrees(
            """
            namespace N {
                class M5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa) {
                }

                class M6(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa, bbb) {
                }
            }

            class M7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) {
            }
            """,
            """
            namespace N {
                class M5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                    : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa) { }

                class M6(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                    : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa, bbb) { }
            }

            class M7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb(
                aaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            ) { }
            """
        );

    /// <summary>
    ///     The issue's shape and its family: a class, a record and a struct whose base type's arguments are chopped by a
    ///     comment or an author's break keep <c>: B(</c> on the declaration's line; a list too long for the line breaks before
    ///     the <c>:</c> when it then fits, and keeps <c>: B(</c> and chops when it does not; interfaces after it and a
    ///     constraint, at two depths.
    /// </summary>
    [Fact]
    public void ChoppedArguments_KeepTheColonOnTheLine_WrapIfLong() =>
        Agrees(
            """
            class L(int a, int b) : B(a, b // e
            ) {
            }

            class L2(int a, int b) : B(a, b /*e*/
            ) {
            }

            record R(int a, int b) : B(a, b // e
            );

            struct S(int a, int b) : I(a, b // e
            ) {
            }

            class L3(int a, int b) : B(a, b // e
            ), I1, I2 {
            }

            class L4<T>(int a, int b) : B(a, b // e
            ) where T : class {
            }

            class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) {
            }

            class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd) {
            }

            class L8(int a) : B(a,
                b) {
            }

            namespace N {
                class L(int a, int b) : B(a, b // e
                ) {
                }

                class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd) {
                }

                record R(int a, int b) : B(a, b // e
                ), I1;
            }
            """,
            """
            class L(int a, int b) : B(
                a,
                b // e
            ) { }

            class L2(int a, int b) : B(
                a,
                b /*e*/
            ) { }

            record R(int a, int b) : B(
                a,
                b // e
            );

            struct S(int a, int b) : I(
                a,
                b // e
            ) { }

            class L3(int a, int b) : B(
                a,
                b // e
            ), I1, I2 { }

            class L4<T>(int a, int b) : B(
                a,
                b // e
            ) where T : class { }

            class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb)
                : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) { }

            class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                aaaaaaaaaaaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbbbbbbbbb,
                cccccccccccccccccccccccccccccc,
                ddddddddddddddddddddddddddd
            ) { }

            class L8(int a) : B(
                a,
                b
            ) { }

            namespace N {
                class L(int a, int b) : B(
                    a,
                    b // e
                ) { }

                class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                    aaaaaaaaaaaaaaaaaaaaaa,
                    bbbbbbbbbbbbbbbbbbbbbbbbbb,
                    cccccccccccccccccccccccccccccc,
                    ddddddddddddddddddddddddddd
                ) { }

                record R(int a, int b) : B(
                    a,
                    b // e
                ), I1;
            }
            """,
            ("skala_wrap_extends_list_style", "wrap_if_long")
        );

    /// <summary>
    ///     A head too long for <c>: B(</c> breaks before the colon; a chopped base type followed by interfaces nests its
    ///     arguments from the extends list's continuation line; a constructor initializer is unchanged; three depths.
    /// </summary>
    [Fact]
    public void HeadsInterfacesAndDepths_WrapIfLong() =>
        Agrees(
            """
            class M1VeryLongClassNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(int aaaaaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbb) {
            }

            class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbb {
            }

            class M3(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa), IAaaaaaaaaaaaaaaaaaaaaaaaaa, IBbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb {
            }

            class M4(
                int a,
                int b
            ) : B(a, b) {
            }

            class M5(int a, int b) : B(a, b // e
            ), I1 {
            }

            class C : X {
                C(int a, int b) : base(a, b // e
                ) {
                }

                C(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccc) : base(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) {
                }
            }

            namespace N {
                namespace O {
                    class L3(int a, int b) : B(a, b // e
                    ), I1, I2 {
                    }

                    class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbb {
                    }

                    class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) {
                    }
                }
            }
            """,
            """
            class M1VeryLongClassNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(int aaaaaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbb)
                : Bbbbbbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbb) { }

            class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                aaaaaaaaaaaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbbbbbbbbb,
                cccccccccccccccccccccccccccccc,
                ddddddddddddddddddddddddddd
            ), IAaaaa, IBbbbb { }

            class M3(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa),
                IAaaaaaaaaaaaaaaaaaaaaaaaaa, IBbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb { }

            class M4(
                int a,
                int b
            ) : B(a, b) { }

            class M5(int a, int b) : B(
                a,
                b // e
            ), I1 { }

            class C : X {
                C(int a, int b) : base(
                    a,
                    b // e
                ) { }

                C(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccc) : base(
                    aaaaaaaaaaaaaaaaaaaaaa,
                    bbbbbbbbbbbbbbbbbbbbbbbbbb
                ) { }
            }

            namespace N {
                namespace O {
                    class L3(int a, int b) : B(
                        a,
                        b // e
                    ), I1, I2 { }

                    class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                        aaaaaaaaaaaaaaaaaaaaaa,
                        bbbbbbbbbbbbbbbbbbbbbbbbbb,
                        cccccccccccccccccccccccccccccc,
                        ddddddddddddddddddddddddddd
                    ), IAaaaa, IBbbbb { }

                    class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb)
                        : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) { }
                }
            }
            """,
            ("skala_wrap_extends_list_style", "wrap_if_long")
        );

    /// <summary>
    ///     Generic base types, a comma inside the arguments, a <c>record struct</c>, an author's break before and after
    ///     the colon, and a lambda argument.
    /// </summary>
    [Fact]
    public void GenericAndAuthorBrokenShapes_WrapIfLong() =>
        Agrees(
            """
            class G : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, Cccccccccccccccccccccccccccccccc>, IBbbbbbbbbbbbbbbb {
            }

            class G2 : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>, IBbbbbbbbbbbbbbbb {
            }

            class P(int a) : B(a, // e
                a), I1, I2 {
            }

            record struct RS(int a, int b) : I(a, b // e
            ), I2;

            class X(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbbb {
            }

            class Y(int a, int b)
                : B(a, b) {
            }

            class Z(int a, int b) :
                B(a, b) {
            }

            class Q(int a, int b) : B(x => {
                return a;
            }) {
            }

            namespace N {
                class L(int a, int b) : B(a, b // e
                ), I1, I2 {
                }

                class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) {
                }
            }
            """,
            """
            class G : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
                Cccccccccccccccccccccccccccccccc>, IBbbbbbbbbbbbbbbb { }

            class G2 : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>, IBbbbbbbbbbbbbbbb { }

            class P(int a) : B(
                a, // e
                a
            ), I1, I2 { }

            record struct RS(int a, int b) : I(
                a,
                b // e
            ), I2;

            class X(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                aaaaaaaaaaaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbbbbbbbbb,
                cccccccccccccccccccccccccccccc,
                ddddddddddddddddddddddddddd
            ), IAaaaa, IBbbbbb { }

            class Y(int a, int b)
                : B(a, b) { }

            class Z(int a, int b) :
                B(a, b) { }

            class Q(int a, int b) : B(x => { return a; }) { }

            namespace N {
                class L(int a, int b) : B(
                    a,
                    b // e
                ), I1, I2 { }

                class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb)
                    : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) { }
            }
            """,
            ("skala_wrap_extends_list_style", "wrap_if_long")
        );

    /// <summary>A one-argument base type and a too-long argument at two depths.</summary>
    [Fact]
    public void OneArgumentAndTooLongArguments_WrapIfLong() =>
        Agrees(
            """
            namespace N {
                class M5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa) {
                }

                class M6(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa, bbb) {
                }
            }

            class M7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) {
            }
            """,
            """
            namespace N {
                class M5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                    : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa) { }

                class M6(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                    : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa, bbb) { }
            }

            class M7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb(
                aaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            ) { }
            """,
            ("skala_wrap_extends_list_style", "wrap_if_long")
        );

    /// <summary>
    ///     The issue's shape and its family: a class, a record and a struct whose base type's arguments are chopped by a
    ///     comment or an author's break keep <c>: B(</c> on the declaration's line; a list too long for the line breaks before
    ///     the <c>:</c> when it then fits, and keeps <c>: B(</c> and chops when it does not; interfaces after it and a
    ///     constraint, at two depths.
    /// </summary>
    [Fact]
    public void ChoppedArguments_KeepTheColonOnTheLine_ChopAlways() =>
        Agrees(
            """
            class L(int a, int b) : B(a, b // e
            ) {
            }

            class L2(int a, int b) : B(a, b /*e*/
            ) {
            }

            record R(int a, int b) : B(a, b // e
            );

            struct S(int a, int b) : I(a, b // e
            ) {
            }

            class L3(int a, int b) : B(a, b // e
            ), I1, I2 {
            }

            class L4<T>(int a, int b) : B(a, b // e
            ) where T : class {
            }

            class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) {
            }

            class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd) {
            }

            class L8(int a) : B(a,
                b) {
            }

            namespace N {
                class L(int a, int b) : B(a, b // e
                ) {
                }

                class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd) {
                }

                record R(int a, int b) : B(a, b // e
                ), I1;
            }
            """,
            """
            class L(int a, int b) : B(
                a,
                b // e
            ) { }

            class L2(int a, int b) : B(
                a,
                b /*e*/
            ) { }

            record R(int a, int b) : B(
                a,
                b // e
            );

            struct S(int a, int b) : I(
                a,
                b // e
            ) { }

            class L3(int a, int b) : B(
                    a,
                    b // e
                ),
                I1,
                I2 { }

            class L4<T>(int a, int b) : B(
                a,
                b // e
            ) where T : class { }

            class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb)
                : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) { }

            class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                aaaaaaaaaaaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbbbbbbbbb,
                cccccccccccccccccccccccccccccc,
                ddddddddddddddddddddddddddd
            ) { }

            class L8(int a) : B(
                a,
                b
            ) { }

            namespace N {
                class L(int a, int b) : B(
                    a,
                    b // e
                ) { }

                class L7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                    aaaaaaaaaaaaaaaaaaaaaa,
                    bbbbbbbbbbbbbbbbbbbbbbbbbb,
                    cccccccccccccccccccccccccccccc,
                    ddddddddddddddddddddddddddd
                ) { }

                record R(int a, int b) : B(
                        a,
                        b // e
                    ),
                    I1;
            }
            """,
            ("skala_wrap_extends_list_style", "chop_always")
        );

    /// <summary>
    ///     A head too long for <c>: B(</c> breaks before the colon; a chopped base type followed by interfaces nests its
    ///     arguments from the extends list's continuation line; a constructor initializer is unchanged; three depths.
    /// </summary>
    [Fact]
    public void HeadsInterfacesAndDepths_ChopAlways() =>
        Agrees(
            """
            class M1VeryLongClassNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(int aaaaaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbb) {
            }

            class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbb {
            }

            class M3(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa), IAaaaaaaaaaaaaaaaaaaaaaaaaa, IBbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb {
            }

            class M4(
                int a,
                int b
            ) : B(a, b) {
            }

            class M5(int a, int b) : B(a, b // e
            ), I1 {
            }

            class C : X {
                C(int a, int b) : base(a, b // e
                ) {
                }

                C(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccc) : base(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb) {
                }
            }

            namespace N {
                namespace O {
                    class L3(int a, int b) : B(a, b // e
                    ), I1, I2 {
                    }

                    class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbb {
                    }

                    class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) {
                    }
                }
            }
            """,
            """
            class M1VeryLongClassNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeee(int aaaaaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbb)
                : Bbbbbbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbb) { }

            class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                    aaaaaaaaaaaaaaaaaaaaaa,
                    bbbbbbbbbbbbbbbbbbbbbbbbbb,
                    cccccccccccccccccccccccccccccc,
                    ddddddddddddddddddddddddddd
                ),
                IAaaaa,
                IBbbbb { }

            class M3(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa),
                IAaaaaaaaaaaaaaaaaaaaaaaaaa,
                IBbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb { }

            class M4(
                int a,
                int b
            ) : B(a, b) { }

            class M5(int a, int b) : B(
                    a,
                    b // e
                ),
                I1 { }

            class C : X {
                C(int a, int b) : base(
                    a,
                    b // e
                ) { }

                C(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccc) : base(
                    aaaaaaaaaaaaaaaaaaaaaa,
                    bbbbbbbbbbbbbbbbbbbbbbbbbb
                ) { }
            }

            namespace N {
                namespace O {
                    class L3(int a, int b) : B(
                            a,
                            b // e
                        ),
                        I1,
                        I2 { }

                    class M2(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                            aaaaaaaaaaaaaaaaaaaaaa,
                            bbbbbbbbbbbbbbbbbbbbbbbbbb,
                            cccccccccccccccccccccccccccccc,
                            ddddddddddddddddddddddddddd
                        ),
                        IAaaaa,
                        IBbbbb { }

                    class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb)
                        : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) { }
                }
            }
            """,
            ("skala_wrap_extends_list_style", "chop_always")
        );

    /// <summary>
    ///     Generic base types, a comma inside the arguments, a <c>record struct</c>, an author's break before and after
    ///     the colon, and a lambda argument.
    /// </summary>
    [Fact]
    public void GenericAndAuthorBrokenShapes_ChopAlways() =>
        Agrees(
            """
            class G : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, Cccccccccccccccccccccccccccccccc>, IBbbbbbbbbbbbbbbb {
            }

            class G2 : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>, IBbbbbbbbbbbbbbbb {
            }

            class P(int a) : B(a, // e
                a), I1, I2 {
            }

            record struct RS(int a, int b) : I(a, b // e
            ), I2;

            class X(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccc, ddddddddddddddddddddddddddd), IAaaaa, IBbbbbb {
            }

            class Y(int a, int b)
                : B(a, b) {
            }

            class Z(int a, int b) :
                B(a, b) {
            }

            class Q(int a, int b) : B(x => {
                return a;
            }) {
            }

            namespace N {
                class L(int a, int b) : B(a, b // e
                ), I1, I2 {
                }

                class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) {
                }
            }
            """,
            """
            class G : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
                    Cccccccccccccccccccccccccccccccc>,
                IBbbbbbbbbbbbbbbb { }

            class G2 : IDictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                    Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>,
                IBbbbbbbbbbbbbbbb { }

            class P(int a) : B(
                    a, // e
                    a
                ),
                I1,
                I2 { }

            record struct RS(int a, int b) : I(
                    a,
                    b // e
                ),
                I2;

            class X(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(
                    aaaaaaaaaaaaaaaaaaaaaa,
                    bbbbbbbbbbbbbbbbbbbbbbbbbb,
                    cccccccccccccccccccccccccccccc,
                    ddddddddddddddddddddddddddd
                ),
                IAaaaa,
                IBbbbbb { }

            class Y(int a, int b)
                : B(a, b) { }

            class Z(int a, int b) :
                B(a, b) { }

            class Q(int a, int b) : B(x => { return a; }) { }

            namespace N {
                class L(int a, int b) : B(
                        a,
                        b // e
                    ),
                    I1,
                    I2 { }

                class L5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbb)
                    : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbb) { }
            }
            """,
            ("skala_wrap_extends_list_style", "chop_always")
        );

    /// <summary>A one-argument base type and a too-long argument at two depths.</summary>
    [Fact]
    public void OneArgumentAndTooLongArguments_ChopAlways() =>
        Agrees(
            """
            namespace N {
                class M5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa) {
                }

                class M6(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa, bbb) {
                }
            }

            class M7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb) {
            }
            """,
            """
            namespace N {
                class M5(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                    : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa) { }

                class M6(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb)
                    : Bbbbbbbbbbbbbbbbbbbbbbbb(aaaaaaaaaaaaa, bbb) { }
            }

            class M7(int aaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb) : Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb(
                aaaaaaaaaaaaa,
                bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            ) { }
            """,
            ("skala_wrap_extends_list_style", "chop_always")
        );
}
