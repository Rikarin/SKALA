namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #372 (SK-DIV-0113, and SK-DIV-0098 with it): <c>if_owner_is_single_line</c> is read off
///     the output. The arrow of an expression body breaks whenever any break before it was taken —
///     whoever took it — and never because <c>=&gt; body;</c> is too wide for the head's last line
///     alone. Every expected string is <c>jb cleanupcode</c>'s own output for the input, measured
///     2026-09-17 with <c>Testing ask</c>; <c>constructs/breaks/expression-body-after-a-broken-head.cs</c>
///     holds the same shapes as a fixture.
/// </summary>
/// <remarks>
///     ⚠ The Nightly fuzzer's seed 7611825995831206751 found the shape the first class pins: pass
///     one filled the type parameter list and kept the arrow inline, because the fill's break was
///     not in the source and the arrow read the parameter list's group alone; pass two read the same
///     break as the author's and moved the body. The issue's corrected text called pass one right on
///     the width — line three is 113 columns — and the oracle refutes the width reading: given pass
///     one's output it breaks the arrow, and given every other multi-line head below it breaks the
///     arrow too, with the body fitting on the head's last line in each.
/// </remarks>
public sealed class ExpressionBodyAfterABrokenHeadTests {
    /// <summary>
    ///     The fuzzer's three lines. ⚠ The head wrap is not the oracle's: the oracle breaks between
    ///     the return type and <c>M113</c> and declines both lists (SK-DIV-0024, deliberate), where
    ///     Skala fills the type parameter list. What this test pins is the arrow, and the oracle's
    ///     answer for the arrow is measured on the head Skala writes: given pass one's output as
    ///     input, <c>jb cleanupcode</c> returns exactly the expected string, and returns the expected
    ///     string unchanged when given it.
    /// </summary>
    [Fact]
    public void AFilledTypeParameterList_BreaksTheArrow_OnPassOne() {
        const string source = """
                              public  sealed record T2 : IDisposable, IEnumerable<object> {
                                private  IReadOnlyDictionary<IEnumerable<IReadOnlyList<char>>, IEnumerable<IReadOnlyList<char>>> M113<T110, T111, T112>() where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112 > => builder.Items;
                              }
                              """;

        const string settled = """
                               public sealed record T2 : IDisposable, IEnumerable<object> {
                                   private IReadOnlyDictionary<IEnumerable<IReadOnlyList<char>>, IEnumerable<IReadOnlyList<char>>> M113<T110, T111,
                                       T112>() where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112> =>
                                       builder.Items;
                               }
                               """;

        Oracle.Agrees(source, settled);
        Oracle.Agrees(settled, settled);
    }

    /// <summary>The oracle's own answer for the three lines is a fixed point for Skala as well.</summary>
    [Fact]
    public void TheOraclesHeadWrap_IsLeftAlone() =>
        Oracle.Agrees(
            """
            public sealed record T2 : IDisposable, IEnumerable<object> {
                private IReadOnlyDictionary<IEnumerable<IReadOnlyList<char>>, IEnumerable<IReadOnlyList<char>>>
                    M113<T110, T111, T112>() where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112> =>
                    builder.Items;
            }
            """,
            """
            public sealed record T2 : IDisposable, IEnumerable<object> {
                private IReadOnlyDictionary<IEnumerable<IReadOnlyList<char>>, IEnumerable<IReadOnlyList<char>>>
                    M113<T110, T111, T112>() where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112> =>
                    builder.Items;
            }
            """
        );

    [Fact]
    public void OneWhereClause_AfterAKeptTypeParameterBreak() =>
        Oracle.Agrees(
            """
            public sealed record T2 : IDisposable, IEnumerable<object> {
                private IReadOnlyDictionary<IEnumerable<IReadOnlyList<char>>, IEnumerable<IReadOnlyList<char>>> M113<T110, T111,
                    T112>() where T111 : notnull, IEquatable<T111> => builder.Items;
            }
            """,
            """
            public sealed record T2 : IDisposable, IEnumerable<object> {
                private IReadOnlyDictionary<IEnumerable<IReadOnlyList<char>>, IEnumerable<IReadOnlyList<char>>> M113<T110, T111,
                    T112>() where T111 : notnull, IEquatable<T111> =>
                    builder.Items;
            }
            """
        );

    /// <summary>
    ///     Two clauses and no type parameter break: the head is too wide, the first <c>where</c> moves
    ///     down, and the arrow follows it although <c>=&gt; builder.Items;</c> fits where it was.
    /// </summary>
    [Fact]
    public void TwoWhereClauses_MovedDown_BreakTheArrow() =>
        Oracle.Agrees(
            """
            public sealed record T2 : IDisposable, IEnumerable<object> {
                private IReadOnlyDictionary<object, object> M113<T110, T111, T112>() where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112> => builder.Items;
            }
            """,
            """
            public sealed record T2 : IDisposable, IEnumerable<object> {
                private IReadOnlyDictionary<object, object> M113<T110, T111, T112>()
                    where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112> =>
                    builder.Items;
            }
            """
        );

    [Fact]
    public void ABodyThatNeedsTheBreakOnItsOwn_TakesTheSameShape() =>
        Oracle.Agrees(
            """
            public sealed record T2 : IDisposable, IEnumerable<object> {
                private IReadOnlyDictionary<object, object> M113<T110, T111, T112>() where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112> => builder.ItemsWithAVeryLongName.SelectMany(x => x);
            }
            """,
            """
            public sealed record T2 : IDisposable, IEnumerable<object> {
                private IReadOnlyDictionary<object, object> M113<T110, T111, T112>()
                    where T111 : notnull, IEquatable<T111> where T112 : unmanaged, IEquatable<T112> =>
                    builder.ItemsWithAVeryLongName.SelectMany(x => x);
            }
            """
        );

    /// <summary>
    ///     Four kept breaks, none of which is a chopped parameter list: inside the type parameter list
    ///     (SK-DIV-0104), before a second <c>where</c> (SK-DIV-0098), inside the parameter list — which
    ///     chops it — and between the return type and the name. Each head is short, each body fits,
    ///     each arrow breaks.
    /// </summary>
    [Fact]
    public void AKeptBreakAnywhereInTheHead_BreaksTheArrow() =>
        Oracle.Agrees(
            """
            public sealed record T2 : IDisposable, IEnumerable<object> {
                private IReadOnlyDictionary<object, object> M117<T110, T111,
                    T112>() where T111 : notnull => builder.Items;

                private IReadOnlyDictionary<object, object> M118<T110, T111, T112>() where T111 : notnull
                    where T112 : unmanaged => builder.Items;

                private IReadOnlyDictionary<object, object> M119<T110, T111, T112>(int a,
                    int b) where T111 : notnull => builder.Items;

                private IReadOnlyDictionary<object, object>
                    M120<T110, T111, T112>() where T111 : notnull => builder.Items;
            }
            """,
            """
            public sealed record T2 : IDisposable, IEnumerable<object> {
                private IReadOnlyDictionary<object, object> M117<T110, T111,
                    T112>() where T111 : notnull =>
                    builder.Items;

                private IReadOnlyDictionary<object, object> M118<T110, T111, T112>()
                    where T111 : notnull
                    where T112 : unmanaged =>
                    builder.Items;

                private IReadOnlyDictionary<object, object> M119<T110, T111, T112>(
                    int a,
                    int b
                ) where T111 : notnull =>
                    builder.Items;

                private IReadOnlyDictionary<object, object>
                    M120<T110, T111, T112>() where T111 : notnull =>
                    builder.Items;
            }
            """
        );

    /// <summary>
    ///     Where the head begins. A kept break after a modifier is inside it; an attribute list on
    ///     its own line is not — and neither is one the author put on the declaration's line, which
    ///     the oracle moves off it. A constructor's chopped list and a local function's kept type
    ///     parameter break are the same rule on two more owners; a one-line head keeps its body.
    /// </summary>
    [Fact]
    public void TheHeadStartsAfterTheAttributes() =>
        Oracle.Agrees(
            """
            public sealed record T2 {
                [Obsolete]
                public int M() => 1;

                [Obsolete] public int N() => 1;

                public
                int O() => 1;

                public T2(int aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccc) => builder.Items = 1;

                void Q() {
                    int Local<T110, T111,
                        T112>() where T111 : notnull => 1;
                }

                private IReadOnlyDictionary<object, object> Two<T110>() where T110 : class, new() => builder.Items;

                public int P {
                    get => 1;
                }
            }
            """,
            """
            public sealed record T2 {
                [Obsolete]
                public int M() => 1;

                [Obsolete]
                public int N() => 1;

                public
                    int O() =>
                    1;

                public T2(
                    int aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                    int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
                    int cccccccccccccccccccc
                ) =>
                    builder.Items = 1;

                void Q() {
                    int Local<T110, T111,
                        T112>() where T111 : notnull =>
                        1;
                }

                private IReadOnlyDictionary<object, object> Two<T110>() where T110 : class, new() => builder.Items;

                public int P {
                    get => 1;
                }
            }
            """
        );
}
