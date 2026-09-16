namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #369: four shapes where Skala joined or re-indented a break the oracle keeps, found beside
///     #368. Every expected string here is <c>jb cleanupcode</c>'s own output for the input, measured
///     under the repository's export; the <c>constructs/breaks/</c> fixtures named in each class hold
///     the wider set of shapes and are the sweep's copy of the same assertion.
/// </summary>
static class Oracle {
    /// <summary>
    ///     Skala's output equals the oracle's, and a second pass changes nothing. Trailing newlines are
    ///     not part of the comparison: raw literals have none and the formatter keeps the input's.
    /// </summary>
    public static void Agrees(string source, string expected) {
        var once = Format.Text(source);
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = Format.Text(once);
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }
}

/// <summary>
///     SK-DIV-0101: a body that opens with a parenthesis the author broke after puts the <c>(</c> at the
///     owner's own indent and its contents one level in. <c>constructs/breaks/chopped-parenthesis-body.cs</c>.
/// </summary>
public sealed class ChoppedParenthesisBodyTests {
    [Fact]
    public void AfterTheArrow_TheParenthesisTakesTheMembersIndent() =>
        Oracle.Agrees(
            """
            class T {
                object A() =>
                    (
                        1, 2);

                object B() => (
            1, 2);

                object C() =>
                    (
                        a + b);
            }
            """,
            """
            class T {
                object A() =>
                (
                    1, 2);

                object B() =>
                (
                    1, 2);

                object C() =>
                (
                    a + b);
            }
            """
        );

    [Fact]
    public void TheReceiverOfAMemberAccess_AndTheConditionOfATernary_Qualify() =>
        Oracle.Agrees(
            """
            class T {
                object A() =>
                    (
                        first, second).ToString();

                object C() =>
                    (
                        a) ? b : c;

                object D() =>
                    (
                        a, b) switch {
                        _ => 1
                    };
            }
            """,
            """
            class T {
                object A() =>
                (
                    first, second).ToString();

                object C() =>
                (
                    a)
                    ? b
                    : c;

                object D() =>
                (
                    a, b) switch {
                    _ => 1
                };
            }
            """
        );

    [Fact]
    public void ABinaryOperand_ACast_AndADoubleParenthesis_KeepTheContinuation() =>
        Oracle.Agrees(
            """
            class T {
                object B() =>
                    (
                        a) + b;

                object F() =>
                    (int)(
                        x);

                object H() =>
                    ((
                        1, 2), 3);
            }
            """,
            """
            class T {
                object B() =>
                    (
                        a)
                    + b;

                object F() =>
                    (int)(
                        x);

                object H() =>
                    ((
                        1, 2), 3);
            }
            """
        );

    [Fact]
    public void AfterAnEquals_ALambdaArrow_AndAReturn_TheParenthesisTakesTheStatementsIndent() =>
        Oracle.Agrees(
            """
            class T {
                void M() {
                    var t =
                        (
                            1, 2);
                    var u = (
                        1, 2);
                    System.Func<object> f = () =>
                        (
                            1, 2);
                }

                object H() {
                    return
                        (
                            1, 2);
                }
            }
            """,
            """
            class T {
                void M() {
                    var t =
                    (
                        1, 2);
                    var u = (
                        1, 2);
                    System.Func<object> f = () =>
                    (
                        1, 2);
                }

                object H() {
                    return
                    (
                        1, 2);
                }
            }
            """
        );
}

/// <summary>
///     SK-DIV-0102: a statement condition broken right after its <c>(</c> lands one continuation level
///     in from the statement, not on the column after the parenthesis.
///     <c>constructs/breaks/condition-after-lpar.cs</c>.
/// </summary>
public sealed class ConditionAfterLparTests {
    [Fact]
    public void ABreakRightAfterTheParenthesis_FallsBackToTheContinuation() =>
        Oracle.Agrees(
            """
            class T {
                void M(bool c, int n, int[] xs) {
                    while (
                    c) {
                        n++;
                    }

                    switch (
                    n) {
                        case 1:
                            break;
                    }

                    foreach (
                        var x in xs) {
                        n++;
                    }

                    lock (
                        xs) {
                        n++;
                    }
                }
            }
            """,
            """
            class T {
                void M(bool c, int n, int[] xs) {
                    while (
                        c) {
                        n++;
                    }

                    switch (
                        n) {
                        case 1:
                            break;
                    }

                    foreach (
                        var x in xs) {
                        n++;
                    }

                    lock (
                        xs) {
                        n++;
                    }
                }
            }
            """
        );

    [Fact]
    public void ABreakInsideTheCondition_StillAlignsToTheParenthesis() =>
        Oracle.Agrees(
            """
            class T {
                void M(bool c, int n) {
                    while (c
                        && n > 0) {
                        n--;
                    }

                    switch (n
                        + 1) {
                        case 1:
                            break;
                    }
                }
            }
            """,
            """
            class T {
                void M(bool c, int n) {
                    while (c
                           && n > 0) {
                        n--;
                    }

                    switch (n
                            + 1) {
                        case 1:
                            break;
                    }
                }
            }
            """
        );
}

/// <summary>
///     SK-DIV-0103: a break after a parameter default's <c>=</c> is kept, a collection expression after
///     it included; the value takes a level past the parameter, and the list chops.
///     <c>constructs/breaks/parameter-default-after-eq.cs</c>.
/// </summary>
public sealed class ParameterDefaultAfterEqTests {
    [Fact]
    public void TheBreakIsKept_TheValueIndents_AndTheListChops() =>
        Oracle.Agrees(
            """
            class T {
                void A(int[] a =
                    [1, 2]) { }

                void B(int a =
                    5) { }

                void D(int a
                    = 5) { }

                int F(int a =
                    5) => a;
            }
            """,
            """
            class T {
                void A(
                    int[] a =
                        [1, 2]
                ) { }

                void B(
                    int a =
                        5
                ) { }

                void D(
                    int a
                        = 5
                ) { }

                int F(
                    int a =
                        5
                ) =>
                    a;
            }
            """
        );

    [Fact]
    public void ABracketThatFits_KeepsTheEqualsBreak_InEveryOwner() =>
        Oracle.Agrees(
            """
            class T {
                int[] field =
                    [1, 2];

                int[] Prop { get; } =
                    [1, 2];

                void M() {
                    int[] local =
                        [1, 2];
                    int[] other =
                        [
                            1, 2
                        ];
                }
            }
            """,
            """
            class T {
                int[] field =
                    [1, 2];

                int[] Prop { get; } =
                    [1, 2];

                void M() {
                    int[] local =
                        [1, 2];
                    int[] other = [
                        1, 2
                    ];
                }
            }
            """
        );

    [Fact]
    public void AnInitializerElement_AndAnAttributeArgument_ChopTheirListToo() =>
        Oracle.Agrees(
            """
            class T {
                [System.Obsolete(Message =
                    "x")]
                void A() { }

                void B() {
                    var o = new T { X =
                        1, Y = 2 };
                }

                int X, Y;
            }
            """,
            """
            class T {
                [System.Obsolete(
                    Message =
                        "x"
                )]
                void A() { }

                void B() {
                    var o = new T {
                        X =
                            1,
                        Y = 2
                    };
                }

                int X, Y;
            }
            """
        );
}

/// <summary>
///     SK-DIV-0104: under <c>wrap_before_comma = false</c> a break before a comma is kept in a tuple and
///     a type parameter list and joined everywhere a wrap style re-lays the commas.
///     <c>constructs/breaks/break-before-comma.cs</c>.
/// </summary>
public sealed class BreakBeforeCommaTests {
    [Fact]
    public void ATuple_KeepsTheBreak_AndTheArrowAboveItBreaks() =>
        Oracle.Agrees(
            """
            class T {
                object A() => ([1, 2]
                    , 3);

                object B() => (1
                    , 2
                    , 3);

                void E() {
                    var t = (1
                        , 2);
                }
            }
            """,
            """
            class T {
                object A() =>
                    ([1, 2]
                        , 3);

                object B() =>
                    (1
                        , 2
                        , 3);

                void E() {
                    var t = (1
                        , 2);
                }
            }
            """
        );

    [Fact]
    public void ATypeParameterList_KeepsTheBreak_OnEitherSideOfTheComma() =>
        Oracle.Agrees(
            """
            class T {
                int G<T1
                    , U>() => 0;

                int H<T1,
                    U>() => 0;
            }
            """,
            """
            class T {
                int G<T1
                    , U>() =>
                    0;

                int H<T1,
                    U>() =>
                    0;
            }
            """
        );

    [Fact]
    public void EveryListWithAWrapStyle_JoinsIt() =>
        Oracle.Agrees(
            """
            class T {
                void D(int a
                    , int b) {
                    F(1
                        , 2);
                    int[] xs = [1
                        , 2];
                    var arr = new[] { 1
                        , 2 };
                    var s = (1, 2) switch { (1, _) => 1
                        , _ => 2 };
                }

                void F(int a, int b) { }
            }

            enum E {
                A
                , B
            }
            """,
            """
            class T {
                void D(int a, int b) {
                    F(1, 2);
                    int[] xs = [1, 2];
                    var arr = new[] { 1, 2 };
                    var s = (1, 2) switch {
                        (1, _) => 1,
                        _ => 2
                    };
                }

                void F(int a, int b) { }
            }

            enum E {
                A,
                B
            }
            """
        );
}
