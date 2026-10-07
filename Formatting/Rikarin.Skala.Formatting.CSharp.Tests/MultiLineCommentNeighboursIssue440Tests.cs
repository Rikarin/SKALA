namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #440, SK-DIV-0205: the neighbours of #435 around a block comment that spans lines. A
///     comment's own line breaks are not the author's break between two tokens, so a ternary holding one
///     chops at both signs instead of keeping one "broken" sign; <c>is</c> and <c>as</c> wrap after the
///     keyword and never before it; an array initializer's <c>{</c> and a collection expression's
///     <c>[</c> keep the first element after such a comment; and a fill measures an item only up to the
///     comment's first line. Every expected string is <c>jb cleanupcode</c>'s own output for the input,
///     and <see cref="Oracle.Agrees" /> asserts the second pass too.
/// </summary>
public sealed class MultiLineCommentNeighboursIssue440Tests {
    const string Long1 = "3333333333, 4444444444, 5555555555, 666666666"
        + "6, 7777777777, 88888888888, 99999999999, 1010"
        + "101010";

    const string Long2 = "someVeryLongReceiverName.SomeVeryLongProperty" + "Name.AnotherVeryLongPropertyName";
    const string Long3 = "someVeryLongReceiverName.SomeVeryLongProperty" + "Name.AnotherVeryLongPropertyNameXYZ";

    const string Long4 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF"
        + "EDCBA_SomeVeryLongPropertyName_AnotherVeryLon"
        + "gPropertyName";

    const string Long5 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF"
        + "EDCBA_SomeVeryLongPropertyName_AnotherVeryLon"
        + "gPropertyNam";

    const string Long6 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF" + "EDCBA_SomeVeryLongPropertyName_Anoth";
    const string Long7 = "someVeryLongReceiverName.SomeVeryLongProperty" + "Name.AnotherVeryLongPropertyNameXYZWVUTS";

    const string Long8 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF"
        + "EDCBA_SomeVeryLongPropertyName_AnotherVeryLon"
        + "g";

    const string Long9 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF" + "EDCBA_SomeVeryLong";
    const string Long10 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF" + "EDCBA_SomeVeryLongPropertyName_AnotherVer";
    const string Long11 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF" + "EDCBA";
    const string Long12 = "someVeryLongPropertyName_AnotherVery";
    const string Long13 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF" + "EDCBA_Some";

    const string Long14 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF"
        + "EDCBA_SomeVeryLongPropertyName_AnotherVeryLon"
        + "gPro";

    const string Long15 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF"
        + "EDCBA_SomeVeryLongPropertyName_AnotherVeryLon"
        + "gPropert";

    const string Long16 = "someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGF"
        + "EDCBA_SomeVeryLongPropertyName_AnotherVeryLon"
        + "gPropertyNamXYZWVU";

    const string Long17 = "11111111, 22222222, 33333333, 44444444, 55555" + "555, 66666666, 77777777, 88888888";

    const string Long18 = "99999999, 10101010, 11111111, 12121212, 13131"
        + "313, 14141414, 15151515, 16161616, 17171717, "
        + "18181818";

    const string Long19 =
        "11111111, 22222222, 33333333, 44444444, 55555" + "555, 66666666, 77777777, 88888888, 99999999";

    const string Long20 = "11111111, 22222222, 33333333, 44444444, 55555"
        + "555, 66666666, 77777777, 88888888, 99999999, "
        + "10101010";

    const string Long21 = "11111111, 22222222, 33333333, 44444444, 55555"
        + "555, 66666666, 77777777, 88888888, 99999999, "
        + "10101010, 11111111";

    /// <summary>
    ///     A ternary with a comment that spans lines anywhere in it chops at both signs; an <c>is</c> or
    ///     <c>as</c> keeps its type after such a comment; an array initializer and a collection
    ///     expression keep their first element after one; an object, a collection and a dictionary
    ///     initializer break after it.
    /// </summary>
    [Fact]
    public void TheIssuesShapes_AndTheirFamily() =>
        Oracle.Agrees(
            $$"""
            class C {
                void M() {
                    var t = c /* a
                      b */ ? 1 : 2;
                    var t2 = c /* a */ ? 1 : 2;
                    var t3 = c ? 1 /* a
                      b */ : 2;
                    var t4 = c ? 1 : 2 /* a
                      b */;
                    var t5 = c /* a
                      b */ ? x ? 1 : 2 : 3;
                    var t6 = Compute(c) /* a
                      b */ ? 1 : 2;
                    bool b1 = o is /* gap
                      gap2 */ string;
                    bool b2 = o as /* gap
                      gap2 */ string;
                    bool b3 = o is /* gap */ string;
                    bool b4 = o /* gap
                      gap2 */ is string;
                    bool b5 = a && o is /* gap
                      gap2 */ string;
                    bool b6 = o is /* gap
                      gap2 */ string && a;
                    bool b7 = o is /* gap
                      gap2 */ { Length: 1 };
                    var z = new[] { /* a
                      b */ 1, 2 };
                    var z2 = new int[] { /* a
                      b */ 1, 2 };
                    var z3 = new List<int> { /* a
                      b */ 1, 2 };
                    int[] z4 = { /* a
                      b */ 1, 2 };
                    var z5 = new Point { /* a
                      b */ X = 1, Y = 2 };
                    int[] z6 = [/* a
                      b */ 1, 2];
                    var z7 = new[] { /* a */ 1, 2 };
                    var z8 = new[] {
                        /* a
                      b */ 1, 2 };
                    var z9 = new[] { 1, 2, /* a
                      b */ 3 };
                    var z10 = new Dictionary<int, int> { /* a
                      b */ [1] = 2 };
                    var z11 = new[] { /* a
                      b */ 1, 2, {{Long1}} };
                }

                bool B(object o) => o is /* gap
                  gap2 */ string;
                bool B2(object o) => o is /* gap
                  gap2 */ string || o is /* gap
                  gap2 */ int;
            }
            """,
            $$"""
            class C {
                void M() {
                    var t = c /* a
                      b */
                        ? 1
                        : 2;
                    var t2 = c /* a */ ? 1 : 2;
                    var t3 = c
                        ? 1 /* a
                          b */
                        : 2;
                    var t4 = c ? 1 : 2 /* a
                      b */;
                    var t5 = c /* a
                      b */
                        ? x ? 1 : 2
                        : 3;
                    var t6 = Compute(c) /* a
                      b */
                        ? 1
                        : 2;
                    bool b1 = o is /* gap
                      gap2 */ string;
                    bool b2 = o as /* gap
                      gap2 */ string;
                    bool b3 = o is /* gap */ string;
                    bool b4 = o /* gap
                      gap2 */ is string;
                    bool b5 = a
                        && o is /* gap
                          gap2 */ string;
                    bool b6 = o is /* gap
                      gap2 */ string
                        && a;
                    bool b7 = o is /* gap
                      gap2 */ { Length: 1 };
                    var z = new[] { /* a
                      b */ 1, 2
                    };
                    var z2 = new int[] { /* a
                      b */ 1, 2
                    };
                    var z3 = new List<int> { /* a
                      b */
                        1, 2
                    };
                    int[] z4 = { /* a
                      b */ 1, 2
                    };
                    var z5 = new Point { /* a
                      b */
                        X = 1, Y = 2
                    };
                    int[] z6 = [ /* a
                      b */ 1, 2
                    ];
                    var z7 = new[] { /* a */ 1, 2 };
                    var z8 = new[] {
                        /* a
                      b */ 1, 2
                    };
                    var z9 = new[] {
                        1, 2, /* a
                          b */ 3
                    };
                    var z10 = new Dictionary<int, int> { /* a
                      b */
                        [1] = 2
                    };
                    var z11 = new[] { /* a
                      b */ 1, 2, {{Long1}}
                    };
                }

                bool B(object o) =>
                    o is /* gap
                      gap2 */ string;

                bool B2(object o) =>
                    o is /* gap
                      gap2 */ string
                    || o is /* gap
                    gap2 */ int;
            }
            """
        );

    /// <summary>
    ///     <c>is</c> and <c>as</c> wrap after the keyword, one level past the operand's line, and an
    ///     author's break before the keyword is kept.
    /// </summary>
    [Fact]
    public void IsAndAs_WrapAfterTheKeyword() =>
        Oracle.Agrees(
            $$"""
            class C {
                void M() {
                    bool b1 = {{Long2}} is SomeVeryLongTypeName;
                    bool b2 = {{Long3}} as SomeVeryLongTypeName;
                    bool b3 = {{Long4}} is SomeVeryLongTypeName;
                    var b4 = {{Long4}} as SomeVeryLongTypeName;
                    bool b5 = {{Long4}} is not SomeVeryLongTypeName;
                    bool b6 = {{Long5}} is { Length: 1 };
                    bool b7 = o
                        is string;
                    bool b8 = o
                        as string;
                    bool b9 = Compute(alphaArgument, betaArgument) /* gap
                      gap2 */ is string;
                    var b10 = o /* a */ is string;
                    bool b11 = aaaa && {{Long6}} is SomeVeryLongTypeName;
                }
            }
            """,
            $$"""
            class C {
                void M() {
                    bool b1 = {{Long2}} is SomeVeryLongTypeName;
                    bool b2 =
                        {{Long3}} as SomeVeryLongTypeName;
                    bool b3 =
                        {{Long4}} is
                            SomeVeryLongTypeName;
                    var b4 =
                        {{Long4}} as
                            SomeVeryLongTypeName;
                    bool b5 =
                        {{Long4}} is
                            not SomeVeryLongTypeName;
                    bool b6 =
                        {{Long5}} is {
                            Length: 1
                        };
                    bool b7 = o
                        is string;
                    bool b8 = o
                        as string;
                    bool b9 = Compute(alphaArgument, betaArgument) /* gap
                      gap2 */ is string;
                    var b10 = o /* a */ is string;
                    bool b11 = aaaa
                        && {{Long6}} is
                            SomeVeryLongTypeName;
                }
            }
            """
        );

    /// <summary>
    ///     The same for a pattern with no break point of its own — <c>not</c>, <c>null</c>, a declaration
    ///     — and a binary pattern still wraps at its own operator.
    /// </summary>
    [Fact]
    public void AnUnbreakablePattern_WrapsAfterIs() =>
        Oracle.Agrees(
            $$"""
            class C {
                void M() {
                    bool b5 = {{Long4}} is not SomeVeryLongTypeName;
                    bool c1 = {{Long5}} is null;
                    bool c2 = {{Long7}} is not null && aaaa;
                    bool c4 = {{Long8}} is string text;
                    bool c5 = {{Long9}} is SomeVeryLongTypeName or AnotherVeryLongTypeName;
                    bool c6 = o is /* a
                      b */ not null;
                    bool c7 = o
                        is not null;
                    bool c8 = o is
                        not null;
                    bool c9 = o is
                        string;
                    if ({{Long5}} is string) { }
                    if ({{Long5}} is not null) { }
                    var d1 = Compute({{Long10}} as string);
                    var d2 = Compute({{Long11}}, {{Long12}} is string);
                    return;
                }

                bool P(object {{Long11}}) => {{Long11}} is string;
                bool Q(object {{Long11}}) => {{Long13}} is string;
            }
            """,
            $$"""
            class C {
                void M() {
                    bool b5 =
                        {{Long4}} is
                            not SomeVeryLongTypeName;
                    bool c1 =
                        {{Long5}} is
                            null;
                    bool c2 = {{Long7}} is not null
                        && aaaa;
                    bool c4 =
                        {{Long8}} is string text;
                    bool c5 = {{Long9}} is SomeVeryLongTypeName
                        or AnotherVeryLongTypeName;
                    bool c6 = o is /* a
                      b */ not null;
                    bool c7 = o
                        is not null;
                    bool c8 = o is
                        not null;
                    bool c9 = o is
                        string;
                    if ({{Long5}} is
                        string) { }

                    if ({{Long5}} is
                        not null) { }

                    var d1 = Compute(
                        {{Long10}} as string
                    );
                    var d2 = Compute(
                        {{Long11}},
                        {{Long12}} is string
                    );
                    return;
                }

                bool P(object {{Long11}}) =>
                    {{Long11}} is string;

                bool Q(object {{Long11}}) =>
                    {{Long13}} is string;
            }
            """
        );

    /// <summary>
    ///     As a statement's whole condition the keyword's break lands on the aligned column; under an
    ///     operator, and after an <c>=</c> or an arrow, one level past the line.
    /// </summary>
    [Fact]
    public void InAHeader_TheBreakLandsOnTheConditionsColumn() =>
        Oracle.Agrees(
            $$"""
            class C {
                void M() {
                    if ({{Long5}} is string) { }
                    if (aaaa && {{Long14}} is string) { }
                    if (Compute({{Long14}} is string)) { }
                    while ({{Long15}} is string) { }
                    if ({{Long5}} as string == null) { }
                }

                bool B => {{Long16}} is string;
                bool D = {{Long16}} is string;
            }
            """,
            $$"""
            class C {
                void M() {
                    if ({{Long5}} is
                        string) { }

                    if (aaaa
                        && {{Long14}} is
                            string) { }

                    if (Compute(
                            {{Long14}} is string
                        )) { }

                    while ({{Long15}} is
                           string) { }

                    if ({{Long5}} as
                            string
                        == null) { }
                }

                bool B =>
                    {{Long16}} is
                        string;

                bool D =
                    {{Long16}} is
                        string;
            }
            """
        );

    /// <summary>
    ///     A fill measures an item up to a comment's first line, and an item that ends in a comment that
    ///     spans lines ends its line: <c>2 /* a</c> / <c>b */,</c> / <c>3</c>.
    /// </summary>
    [Fact]
    public void AFill_MeasuresUpToTheCommentsFirstLine() =>
        Oracle.Agrees(
            $$"""
            class C {
                void M() {
                    var z9 = new[] { 1, 2, /* a
                      b */ 3 };
                    var y1 = new[] { 1, 2 /* a
                      b */, 3 };
                    var y2 = new[] { {{Long17}}, /* a
                      b */ {{Long18}} };
                    var y3 = new[] { {{Long19}}, /* a comment that is long
                      b */ 10101010 };
                    var y4 = new[] { {{Long20}}, 1111 /* a
                      b */, 2 };
                    var y5 = new[] { 1, /* a
                      b */ 2, /* c
                      d */ 3 };
                    var y6 = new[] { 1, 2, 3 } /* a
                      b */;
                    byte[] y7 = [1, 2, /* a
                      b */ 3];
                }
            }
            """,
            $$"""
            class C {
                void M() {
                    var z9 = new[] {
                        1, 2, /* a
                          b */ 3
                    };
                    var y1 = new[] {
                        1, 2 /* a
                          b */,
                        3
                    };
                    var y2 = new[] {
                        {{Long17}}, /* a
                          b */ {{Long18}}
                    };
                    var y3 = new[] {
                        {{Long17}},
                        99999999, /* a comment that is long
                          b */ 10101010
                    };
                    var y4 = new[] {
                        {{Long20}},
                        1111 /* a
                          b */,
                        2
                    };
                    var y5 = new[] {
                        1, /* a
                          b */ 2, /* c
                          d */ 3
                    };
                    var y6 = new[] { 1, 2, 3 } /* a
                      b */;
                    byte[] y7 = [
                        1, 2, /* a
                          b */ 3
                    ];
                }
            }
            """
        );

    /// <summary>
    ///     The same in a collection expression, a list pattern, a nested array and a chopped collection
    ///     initializer.
    /// </summary>
    [Fact]
    public void AFill_InEveryListThatFills() =>
        Oracle.Agrees(
            $$"""
            class C {
                void M() {
                    int[] a1 = [1, 2 /* a
                      b */, 3];
                    int[] a2 = [1, 2, /* a
                      b */ 3, 4];
                    var a3 = new List<int> { 1, 2 /* a
                      b */, 3 };
                    var a5 = new[] { {{Long21}}, /* a
                      b */ 12121212 };
                    var a7 = new[] { 1, /** a
                      b */ 2, 3 };
                    if (x is [1, 2 /* a
                      b */, 3]) { }
                    var a8 = new[] { "a", "b" } /* a
                      b */;
                }
            }
            """,
            $$"""
            class C {
                void M() {
                    int[] a1 = [
                        1, 2 /* a
                          b */,
                        3
                    ];
                    int[] a2 = [
                        1, 2, /* a
                          b */ 3, 4
                    ];
                    var a3 = new List<int> {
                        1,
                        2 /* a
                          b */,
                        3
                    };
                    var a5 = new[] {
                        {{Long20}},
                        11111111, /* a
                          b */ 12121212
                    };
                    var a7 = new[] {
                        1, /** a
                          b */ 2, 3
                    };
                    if (x is [
                            1, 2 /* a
                              b */,
                            3
                        ]) { }

                    var a8 = new[] { "a", "b" } /* a
                      b */;
                }
            }
            """
        );
}
