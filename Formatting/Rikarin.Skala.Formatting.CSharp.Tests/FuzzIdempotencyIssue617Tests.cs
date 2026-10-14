namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #617: three fuzz seeds whose second pass changed the first pass's output, each because a decision read
///     something of the source that pass one's own output does not have. Every expected string is
///     <c>jb cleanupcode</c>'s own output, measured 2026-10-14 with <c>Testing ask</c>.
/// </summary>
public sealed class FuzzIdempotencyIssue617Tests {
    /// <summary>
    ///     Fuzz 14577244967404521905: a list pattern a line comment puts on a line of its own after an <c>is</c> sits one
    ///     level past the operand's line while it comes out on one line, and on the operand's column once it breaks —
    ///     kept after a comma, or too wide. Skala read the level off whether the author had broken the list, so
    ///     <c>[_</c> / <c>, .. var rest]</c>, which joins, went flush on pass one and one level in on pass two.
    /// </summary>
    [Fact]
    public void AListPatternPastALineComment_TakesTheLevelOnlyWhileItStaysWhole() =>
        Oracle.Agrees(
            """
            class C {
                bool P1 => source is
            // c
            [_
            , .. var rest];
                bool P3 => source is // c
            [_
            , .. var rest];
                bool M4() {
                    return source is
            // c
            [_
            , .. var rest];
                }
                void M5() {
                    var b = source is
            // c
            [_
            , .. var rest];
                }
                void M10() {
                    F(source is
            // c
            [_
            , .. var rest]);
                }
                bool A1 => source is
                    // c
            [_
            , .. var rest];
                bool B1 => source is
            // c
            [_,
                .. var rest];
                bool E1 => source is
            // c
            [_, var aaaaaaaaaaaaaaaaaaaaaa, var bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, var cccccccccccccccccccccc, var ddddddddddddd, .. var rest];
                bool G1 => source is
            // c
            [_, var aaaaaaaaaaaaaaaaaaaaaa, var bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, var ccccccccccccccccc
            , .. var rest];
            }
            """,
            """
            class C {
                bool P1 =>
                    source is
            // c
                        [_, .. var rest];

                bool P3 =>
                    source is // c
                        [_, .. var rest];

                bool M4() {
                    return source is
            // c
                        [_, .. var rest];
                }

                void M5() {
                    var b = source is
            // c
                        [_, .. var rest];
                }

                void M10() {
                    F(
                        source is
            // c
                            [_, .. var rest]
                    );
                }

                bool A1 =>
                    source is
                        // c
                        [_, .. var rest];

                bool B1 =>
                    source is
            // c
                    [
                        _,
                        .. var rest
                    ];

                bool E1 =>
                    source is
            // c
                    [
                        _, var aaaaaaaaaaaaaaaaaaaaaa, var bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, var cccccccccccccccccccccc,
                        var ddddddddddddd, .. var rest
                    ];

                bool G1 =>
                    source is
            // c
                        [_, var aaaaaaaaaaaaaaaaaaaaaa, var bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, var ccccccccccccccccc, .. var rest];
            }
            """
        );

    /// <summary>
    ///     Fuzz 12392120797706766071: one argument behind a head wider than #610 measured. The floor of two chopped
    ///     the argument from 44 columns where the oracle breaks the <c>=</c>, and pass two — whose chopped argument
    ///     turns the floor off — broke the <c>=</c>.
    /// </summary>
    [Fact]
    public void OneArgumentBehindAWideHead_BreaksTheEqualsWhileTheValueFitsBelow() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    if (a) {
                        if (b) {
                            IReadOnlyDictionary<decimal, decimal> v124 = Materialise<Guid, StringBuilder>((from item in items where 1.5d select 85306));
                            IReadOnlyDictionary<decimal, decimal> v44 = Materialise<Guid, StringBuilder>(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                            IReadOnlyDictionary<decimal, decimal> v48 = MaterialiseGuidStringBuilderXXXX(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        }
                    }
                }
            }
            """,
            """
            class C {
                void M() {
                    if (a) {
                        if (b) {
                            IReadOnlyDictionary<decimal, decimal> v124 =
                                Materialise<Guid, StringBuilder>((from item in items where 1.5d select 85306));
                            IReadOnlyDictionary<decimal, decimal> v44 =
                                Materialise<Guid, StringBuilder>(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                            IReadOnlyDictionary<decimal, decimal> v48 =
                                MaterialiseGuidStringBuilderXXXX(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Fuzz 718351361052470981 (group C's seed): an array initializer in a switch arm whose head is braces or
    ///     brackets. The writer writes such an arm ahead to decide its arrow, and the elements' line records survived
    ///     the rollback, so the real walk read the first element as spanning lines and broke after it.
    /// </summary>
    [Fact]
    public void AnArmsInitializer_FillsFromItsFirstElement() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var x = state switch { { Kind: 0 } => new byte[] { aaaaaaaaaaaaaaaa, bbbbbbbbbb, cccccccccccccccccccccccccc, dddddddddddddddddddddddd, eeeeeeeeee, ffffffffff, gggggggggggggggggggggggggg, hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh }, _ => buffer[1..] };
                    var y = state switch { { Kind: 0 } => M(new byte[] { aaaaaaaaaaaaaaaa, bbbbbbbbbb, cccccccccccccccccccccccccc, dddddddddddddddddddddddd, eeeeeeeeee, ffffffffff, gggggggggggggggggggggggggg, hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh }), _ => buffer[1..] };
                    var v = state switch { [1, 2] => new byte[] { aaaaaaaaaaaaaaaa, bbbbbbbbbb, cccccccccccccccccccccccccc, dddddddddddddddddddddddd, eeeeeeeeee, ffffffffff, gggggggggggggggggggggggggg, hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh }, _ => buffer[1..] };
                }
            }
            """,
            """
            class C {
                void M() {
                    var x = state switch {
                        { Kind: 0 } => new byte[] {
                            aaaaaaaaaaaaaaaa, bbbbbbbbbb, cccccccccccccccccccccccccc, dddddddddddddddddddddddd, eeeeeeeeee,
                            ffffffffff, gggggggggggggggggggggggggg, hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh
                        },
                        _ => buffer[1..]
                    };
                    var y = state switch {
                        { Kind: 0 } => M(
                            new byte[] {
                                aaaaaaaaaaaaaaaa, bbbbbbbbbb, cccccccccccccccccccccccccc, dddddddddddddddddddddddd, eeeeeeeeee,
                                ffffffffff, gggggggggggggggggggggggggg, hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh
                            }
                        ),
                        _ => buffer[1..]
                    };
                    var v = state switch {
                        [1, 2] => new byte[] {
                            aaaaaaaaaaaaaaaa, bbbbbbbbbb, cccccccccccccccccccccccccc, dddddddddddddddddddddddd, eeeeeeeeee,
                            ffffffffff, gggggggggggggggggggggggggg, hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh
                        },
                        _ => buffer[1..]
                    };
                }
            }
            """
        );

    /// <summary>
    ///     Fuzz 16267341160299312604 (this branch's 30-minute fuzz, also on master): an arm's <c>when</c> the author put
    ///     on a line of its own lifts braces or brackets that span lines, whether or not the arrow breaks; braces that
    ///     join do not lift. Skala lifted only under an arrow broken for width, and put the <c>when</c> a level deeper.
    /// </summary>
    [Fact]
    public void AWhenOnItsOwnLine_LiftsBracesThatSpanLines() =>
        Oracle.Agrees(
            """
            class C {
                object A(object state) {
                    if (a) {
                        if (b) {
                            var v1 = state switch {
                                { Value.Length: > 2, Kind
                                : not null }
                                when (from item in Source where 94339 orderby item.Length descending select @"verbatim\path") => source.Value(@"verbatim\path").Select.First.Items("s"),
                            };
                            var v2 = state switch {
                                { Value.Length: > 2, Kind
                                : not null }
                                when (from item in Source select item) => source,
                            };
                            var v3 = state switch {
                                { Value.Length: > 2, Kind
                                : not null }
                                when ready => source.Value(@"verbatim\path").Select.First.Items("s").Select.First.Items("ssssssssssssssssss"),
                            };
                            var v4 = state switch {
                                {
                                    Value.Length: > 2,
                                    Kind: not null
                                }
                                when (from item in Source where 94339 orderby item.Length descending select @"verbatim\path") => source.Value(@"verbatim\path").Select.First.Items("s"),
                            };
                        }
                    }
                }
            }
            """,
            """
            class C {
                object A(object state) {
                    if (a) {
                        if (b) {
                            var v1 = state switch {
                                {
                                        Value.Length: > 2,
                                        Kind
                                        : not null
                                    }
                                    when (from item in Source where 94339 orderby item.Length descending select @"verbatim\path") =>
                                    source.Value(@"verbatim\path").Select.First.Items("s"),
                            };
                            var v2 = state switch {
                                {
                                        Value.Length: > 2,
                                        Kind
                                        : not null
                                    }
                                    when (from item in Source select item) => source,
                            };
                            var v3 = state switch {
                                {
                                        Value.Length: > 2,
                                        Kind
                                        : not null
                                    }
                                    when ready => source.Value(@"verbatim\path")
                                        .Select.First.Items("s")
                                        .Select.First.Items("ssssssssssssssssss"),
                            };
                            var v4 = state switch {
                                { Value.Length: > 2, Kind: not null }
                                    when (from item in Source where 94339 orderby item.Length descending select @"verbatim\path") =>
                                    source.Value(@"verbatim\path").Select.First.Items("s"),
                            };
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A fresh probe of the same rule, not fitted on: brackets and braces that stay broken lift; braces that join
    ///     do not, whether the <c>when</c> is on their line or on its own.
    /// </summary>
    [Fact]
    public void AWhenOnItsOwnLine_FreshProbe() =>
        Oracle.Agrees(
            """
            class C {
                object A(object state) {
                    return state switch {
                        [1, 2,
                            3]
                        when ready => 1,
                        { Length: 3,
                          Name: "x" }
                        when Compute(alpha, beta) => source,
                        Point { X: 1,
                            Y: 2 } p
                        when p.Z > 0 => p,
                        { Length: 3, Name: "x" } when ready => 2,
                        { Length: 3,
                          Name: "x" } when ready => 3,
                        {
                            Length: 3, Name: "x"
                        }
                        when ready => 4,
                        { Length: 3,
                          Name: "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" }
                        when ready => 5,
                        _ => 0
                    };
                }
            }
            """,
            """
            class C {
                object A(object state) {
                    return state switch {
                        [
                                1, 2,
                                3
                            ]
                            when ready => 1,
                        { Length: 3, Name: "x" }
                            when Compute(alpha, beta) => source,
                        Point { X: 1, Y: 2 } p
                            when p.Z > 0 => p,
                        { Length: 3, Name: "x" } when ready => 2,
                        { Length: 3, Name: "x" } when ready => 3,
                        { Length: 3, Name: "x" }
                            when ready => 4,
                        {
                                Length: 3,
                                Name:
                                "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
                            }
                            when ready => 5,
                        _ => 0
                    };
                }
            }
            """
        );

    /// <summary>
    ///     Fuzz 52566946029857855: <c>T v = () =&gt; value;</c> with the arrow past the margin. An empty parameter list
    ///     has nothing to chop, so keeping the <c>=</c> left the arrow past the margin; pass two, reading the arrow's
    ///     break as the author's, broke the <c>=</c>.
    /// </summary>
    [Fact]
    public void AnEmptyParameterListPastTheMargin_BreaksTheEquals() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    (Memory<double> First, ((decimal First, CancellationToken Second) First, Memory<string> Second) Second) v120 = () => value;
                    Memory<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> v345xx = static () => bbbbb;
                    Memory<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> v346xx = async () => bbbbb;
                }
            }
            """,
            """
            class C {
                void M() {
                    (Memory<double> First, ((decimal First, CancellationToken Second) First, Memory<string> Second) Second) v120 =
                        () => value;
                    Memory<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> v345xx =
                        static () => bbbbb;
                    Memory<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> v346xx =
                        async () => bbbbb;
                }
            }
            """
        );

    /// <summary>
    ///     SK-DIV-0500's residue: <c>xs is</c> / a one-line list pattern, no comment. The oracle keeps the break while the
    ///     list fits whole one level in (#562) and otherwise joins <c>xs is [</c> and breaks inside. Skala kept the break
    ///     whatever the width and broke the list one level in; pass two, finding the list broken, joined it.
    /// </summary>
    [Fact]
    public void AOneLineListBelowTheIs_KeepsItsBreakOnlyWhileItStaysWhole() =>
        Oracle.Agrees(
            """
            class C {
                bool P1 => source is
                        [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];
                bool P2 => source is
                        [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];
                bool M3() {
                    return source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];
                }
                bool M4() {
                    return source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];
                }
                void V5() {
                    var b = source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];
                }
                void V6() {
                    var b = source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];
                }
                void A7() {
                    F(source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest]);
                }
                void A8() {
                    F(source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest]);
                }
                void I9() {
                    if (ready && source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest]) { }
                }
                void I10() {
                    if (ready && source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest]) { }
                }
            }
            """,
            """
            class C {
                bool P1 =>
                    source is
                        [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];

                bool P2 =>
                    source is [
                        aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest
                    ];

                bool M3() {
                    return source is
                        [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];
                }

                bool M4() {
                    return source is
                        [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];
                }

                void V5() {
                    var b = source is
                        [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];
                }

                void V6() {
                    var b = source is
                        [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest];
                }

                void A7() {
                    F(
                        source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest]
                    );
                }

                void A8() {
                    F(
                        source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest]
                    );
                }

                void I9() {
                    if (ready
                        && source is
                            [aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest]) { }
                }

                void I10() {
                    if (ready
                        && source is [
                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, var rest
                        ]) { }
                }
            }
            """
        );

    /// <summary>
    ///     SK-DIV-0501 and SK-DIV-0530's residue, one rule: one argument behind a head wider than fourteen breaks the
    ///     <c>=</c> while <c>12·indent + 10·argument + 4·callee + 2·head</c> stays under 1 010 and the value fits below,
    ///     behind a typed local, a <c>var</c> local, an assignment and a deconstruction alike. A row each side of the
    ///     boundary, two steps in, per owner.
    /// </summary>
    [Fact]
    public void OneArgumentBehindAWideHead_OneBoundaryForEveryOwner() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    if (a) {
                        List<int> v294xxxxxxxxx = Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        List<int> v297xxxxxxxxx = Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        var v1063xxxxxxxxxxxxxx = Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        var v1066xxxxxxxxxxxxxx = Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        v1831xxxxxxxxxxxxxxxxxx = Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        v1834xxxxxxxxxxxxxxxxxx = Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        var (v2599xxxxxxxxx, b) = Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        var (v2602xxxxxxxxx, b) = Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                    }
                }
            }
            """,
            """
            class C {
                void M() {
                    if (a) {
                        List<int> v294xxxxxxxxx =
                            Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        List<int> v297xxxxxxxxx = Cxxxxxxxxxxxxxxxxxxx(
                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                        );
                        var v1063xxxxxxxxxxxxxx =
                            Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        var v1066xxxxxxxxxxxxxx = Cxxxxxxxxxxxxxxxxxxx(
                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                        );
                        v1831xxxxxxxxxxxxxxxxxx =
                            Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        v1834xxxxxxxxxxxxxxxxxx = Cxxxxxxxxxxxxxxxxxxx(
                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                        );
                        var (v2599xxxxxxxxx, b) =
                            Cxxxxxxxxxxxxxxxxxxx(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa);
                        var (v2602xxxxxxxxx, b) = Cxxxxxxxxxxxxxxxxxxx(
                            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                        );
                    }
                }
            }
            """
        );

    /// <summary>
    ///     Fuzz 11861007200823207053: the same break with a block comment between the list's elements, or before its
    ///     <c>]</c>. The comment stays on the list's line, so the list is still one line and the rule above holds: the
    ///     break is kept while the list fits whole one level in (a list line ending at 119) and joined a column later.
    ///     Non-idempotent on adca899a, where the kept break broke the list one level in; fixed by 773eeafa.
    /// </summary>
    [Fact]
    public void AOneLineListBelowTheIs_WithABlockCommentInside_KeepsItsBreakOnlyWhileItStaysWhole() =>
        Oracle.Agrees(
            """
            class C {
                void Seed(object candidate) {
                    var matched = candidate is
            [firstElementPatternName, /* f */  secondElementPatternName, thirdElementPatternName, fourthElementPatternName, fifthElementPatternName];
                }
                void M98(object candidate) {
                    var matched = candidate is
            [firstElementPatternName, /* f */ secondElementPatternName, thirdElementPatternName, fxxxxxxxxxxxxxxxxxxx];
                }
                void M101(object candidate) {
                    var matched = candidate is
            [firstElementPatternName, /* f */ secondElementPatternName, thirdElementPatternName, fxxxxxxxxxxxxxxxxxxxxxx];
                }
                void C98(object candidate) {
                    var matched = candidate is
                        [firstElementPatternName, secondElementPatternName, thirdElementPatternName, fxxxxxxxxxxxxxxxxxxx /* f */];
                }
                void C101(object candidate) {
                    var matched = candidate is
                        [firstElementPatternName, secondElementPatternName, thirdElementPatternName, fxxxxxxxxxxxxxxxxxxxxxx /* f */];
                }
            }
            """,
            """
            class C {
                void Seed(object candidate) {
                    var matched = candidate is [
                        firstElementPatternName, /* f */ secondElementPatternName, thirdElementPatternName,
                        fourthElementPatternName, fifthElementPatternName
                    ];
                }

                void M98(object candidate) {
                    var matched = candidate is
                        [firstElementPatternName, /* f */ secondElementPatternName, thirdElementPatternName, fxxxxxxxxxxxxxxxxxxx];
                }

                void M101(object candidate) {
                    var matched = candidate is [
                        firstElementPatternName, /* f */ secondElementPatternName, thirdElementPatternName, fxxxxxxxxxxxxxxxxxxxxxx
                    ];
                }

                void C98(object candidate) {
                    var matched = candidate is
                        [firstElementPatternName, secondElementPatternName, thirdElementPatternName, fxxxxxxxxxxxxxxxxxxx /* f */];
                }

                void C101(object candidate) {
                    var matched = candidate is [
                        firstElementPatternName, secondElementPatternName, thirdElementPatternName, fxxxxxxxxxxxxxxxxxxxxxx /* f */
                    ];
                }
            }
            """
        );
}
