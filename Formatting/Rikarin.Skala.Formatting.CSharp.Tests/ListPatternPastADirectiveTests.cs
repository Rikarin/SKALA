namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     SK-DIV-0604, the Nightly of 2026-10-31 (fuzz 3751787007369975606 on
///     <c>constructs/wrapping/list-pattern-after-a-broken-is.cs</c>): a preprocessor directive between an <c>is</c> and
///     its list pattern puts the <c>[</c> on a line of its own exactly as a line comment does (SK-DIV-0500), so the list's
///     own layout decides the level, not whether the author broke it.
/// </summary>
public sealed class ListPatternPastADirectiveTests {
    /// <summary>
    ///     <c>xs is</c> / <c>#region fuzz</c> / <c>[1</c> / <c>, 2];</c>: pass one read the broken list off the source and
    ///     wrote the joined list flush with the operand; pass two read a one-line list and stepped it a level in. Every
    ///     row is <c>jb cleanupcode</c>'s own output, measured 2026-10-31 with <c>Testing ask</c> on this exact input.
    /// </summary>
    [Fact]
    public void AListThatJoinsPastADirective_SitsOneLevelPastTheOperandsLine() =>
        Oracle.Agrees(
            """
            class C {
                object D() =>
                    xs is
            #region fuzz
                    [1
            , 2];
            #endregion
                object A() => xs is
            #region r
                        [1
            , 2];
            #endregion
                bool R(object[] xs) {
                    return xs is
            #region r
                        [1
            , 2];
            #endregion
                }
                void V() {
                    var b = source is
            #region r
            [_
            , .. var rest];
            #endregion
                }
                object P() =>
                    xs is
            #pragma warning disable CS0168
                    [1
            , 2];
                void Q() {
                    F(source is
            #pragma warning disable CS0168
            [_
            , .. var rest]);
                }
                bool S => source is
            #if NOPE
              junk junk
            #endif
            [_
            , .. var rest];
            }
            """,
            """
            class C {
                object D() =>
                    xs is

                        #region fuzz

                        [1, 2];

                #endregion

                object A() =>
                    xs is

                        #region r

                        [1, 2];

                #endregion

                bool R(object[] xs) {
                    return xs is

                        #region r

                        [1, 2];

                    #endregion
                }

                void V() {
                    var b = source is

                        #region r

                        [_, .. var rest];

                    #endregion
                }

                object P() =>
                    xs is
            #pragma warning disable CS0168
                        [1, 2];

                void Q() {
                    F(
                        source is
            #pragma warning disable CS0168
                            [_, .. var rest]
                    );
                }

                bool S =>
                    source is
            #if NOPE
              junk junk
            #endif
                        [_, .. var rest];
            }
            """
        );

    /// <summary>
    ///     ⚠ A list that stays broken past a directive: the oracle joins its <c>[</c> onto the directive's line
    ///     (<c>#pragma warning disable CS0168 [</c>, <c>#region r [</c>), which swallows the bracket into the directive
    ///     and no longer compiles. Skala keeps the <c>[</c> on a line of its own at the operand's column — the column the
    ///     oracle's own <c>]</c> and elements imply, and SK-DIV-0500's answer past a line comment.
    /// </summary>
    [Fact]
    public void AListThatStaysBrokenPastADirective_KeepsItsBracketOffTheDirectivesLine() {
        const string expected = """
            class C {
                bool E1 =>
                    source is
            #pragma warning disable CS0168
                    [
                        _, var aaaaaaaaaaaaaaaaaaaaaa, var bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, var cccccccccccccccccccccc,
                        var ddddddddddddd, .. var rest
                    ];

                bool B1 =>
                    source is
            #pragma warning disable CS0168
                    [
                        _,
                        .. var rest
                    ];
            }
            """;
        var once = Format.Text(
            """
            class C {
                bool E1 => source is
            #pragma warning disable CS0168
            [_, var aaaaaaaaaaaaaaaaaaaaaa, var bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, var cccccccccccccccccccccc, var ddddddddddddd, .. var rest];
                bool B1 => source is
            #pragma warning disable CS0168
            [_,
                .. var rest];
            }
            """
        );
        Assert.Equal(expected, once.TrimEnd('\n'));
        Assert.Equal(once, Format.Text(once));
    }
}
