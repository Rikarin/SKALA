using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #370: eight further kept-break shapes measured beside #369. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input under the repository's export, checked through
///     <see cref="Oracle.Agrees" /> for a second-pass fixed point as well; the <c>constructs/breaks/</c>
///     fixture named in each class holds the wider set of shapes.
/// </summary>
/// <remarks>
///     SK-DIV-0108: an indexer's bracketed parameter list takes the declaration keys — chop when an item
///     is multi-line or a delimiter break is kept, <c>]</c> on its own line, the arrow after it broken,
///     a break before a comma joined. It had no plan at all. <c>constructs/breaks/indexer-parameter-list.cs</c>.
/// </remarks>
public sealed class IndexerParameterListTests {
    [Fact]
    public void AKeptBreakAfterADefaultsEquals_ChopsTheList_AndBreaksTheArrow() =>
        Oracle.Agrees(
            """
            class T {
                int this[int a =
                    5] => a;

                int this[object a =
                    null, string b = null] {
                    get => 0;
                }
            }
            """,
            """
            class T {
                int this[
                    int a =
                        5
                ] =>
                    a;

                int this[
                    object a =
                        null,
                    string b = null
                ] {
                    get => 0;
                }
            }
            """
        );

    [Fact]
    public void ABreakBeforeAComma_IsJoined_AndOneAfterIt_Chops() =>
        Oracle.Agrees(
            """
            class T {
                int this[int a
                    , string b] => a;

                int this[int a,
                    string b, object c] => a;
            }
            """,
            """
            class T {
                int this[int a, string b] => a;

                int this[
                    int a,
                    string b,
                    object c
                ] =>
                    a;
            }
            """
        );

    [Fact]
    public void AKeptDelimiterBreak_ChopsTheList() =>
        Oracle.Agrees(
            """
            class T {
                int this[
                    long a] => 0;
            }
            """,
            """
            class T {
                int this[
                    long a
                ] =>
                    0;
            }
            """
        );
}

/// <summary>
///     SK-DIV-0105: the constraints inside one <c>where</c> clause continue on the <c>where</c>'s own
///     column — a kept break on either side of a comma and a wrap the margin forces alike — at every
///     value of the keys that move the <c>where</c>. <c>constructs/breaks/constraint-continuation.cs</c>.
/// </summary>
public sealed class ConstraintContinuationTests {
    const string Source = """
        class T {
            void A<T1>() where T1 : class
                , new() { }

            void B<T1>() where T1 : class,
                new() { }

            void C<T1>() where T1 : System.Collections.Generic.IEnumerable<int>, System.Collections.Generic.IList<int>, System.IDisposable, new() { }
        }
        """;

    static string Under(params (string Key, string Value)[] overrides) {
        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
            [.. overrides.Select(static pair => new KeyValuePair<string, string>(pair.Key, pair.Value))]
        ).Options;
        var once = CSharpFormatter.Format("Test.cs", SourceText.From(Source), options).Formatted;
        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
        return once.TrimEnd('\n');
    }

    [Fact]
    public void UnderTheExport_TheConstraintSitsOnTheWheresColumn() =>
        Oracle.Agrees(
            Source,
            """
            class T {
                void A<T1>()
                    where T1 : class
                    , new() { }

                void B<T1>()
                    where T1 : class,
                    new() { }

                void C<T1>()
                    where T1 : System.Collections.Generic.IEnumerable<int>, System.Collections.Generic.IList<int>,
                    System.IDisposable, new() { }
            }
            """
        );

    [Fact]
    public void WithoutARun_TheClauseStillTakesItsLevel_AndTheConstraintFollowsIt() =>
        Assert.Equal(
            """
            class T {
                void A<T1>()
                    where T1 : class
                    , new() { }

                void B<T1>()
                    where T1 : class,
                    new() { }

                void C<T1>()
                    where T1 : System.Collections.Generic.IEnumerable<int>, System.Collections.Generic.IList<int>,
                    System.IDisposable, new() { }
            }
            """,
            Under(("skala_place_type_constraints_on_same_line", "false"))
        );

    [Fact]
    public void AtAMultiplierOfTwo_TheConstraintFollowsTheWhere() =>
        Assert.Equal(
            """
            class T {
                void A<T1>()
                        where T1 : class
                        , new() { }

                void B<T1>()
                        where T1 : class,
                        new() { }

                void C<T1>()
                        where T1 : System.Collections.Generic.IEnumerable<int>, System.Collections.Generic.IList<int>,
                        System.IDisposable, new() { }
            }
            """,
            Under(("skala_continuous_indent_multiplier", "2"))
        );

    [Fact]
    public void WithTheIndentKeyOff_BothLinesSitOnTheDeclarationsColumn() =>
        Assert.Equal(
            """
            class T {
                void A<T1>()
                where T1 : class
                , new() { }

                void B<T1>()
                where T1 : class,
                new() { }

                void C<T1>()
                where T1 : System.Collections.Generic.IEnumerable<int>, System.Collections.Generic.IList<int>, System.IDisposable,
                new() { }
            }
            """,
            Under(("skala_indent_type_constraints", "false"), ("skala_place_type_constraints_on_same_line", "false"))
        );
}

/// <summary>
///     SK-DIV-0106: under <c>keep_existing_embedded_arrangement</c> a simple embedded statement leaves
///     its header's closing line only when it does not fit there — not because the header is
///     multi-line — and the constructs in the header wrap before it moves.
///     <c>constructs/breaks/embedded-statement-after-multiline-header.cs</c>.
/// </summary>
public sealed class EmbeddedStatementAfterMultilineHeaderTests {
    /// <summary>
    ///     <see cref="Oracle.Agrees" /> with brace insertion off, as the corpus has it: the repository's
    ///     own <c>csharp_prefer_braces</c> would wrap every embedded statement here in a block.
    /// </summary>
    static void Agrees(string source, string expected) {
        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
            [new("csharp_prefer_braces", "false")]
        ).Options;
        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    [Fact]
    public void AKeptHeaderBreak_LeavesTheStatementOnTheClosingLine() =>
        Agrees(
            """
            class T {
                void M(bool c, int n, int[] xs) {
                    while (
                        c) n++;

                    foreach (
                        var x in xs) n++;

                    while (c
                        && n > 0) n--;

                    if (
                        c) n++;
                    else n--;
                }
            }
            """,
            """
            class T {
                void M(bool c, int n, int[] xs) {
                    while (
                        c) n++;

                    foreach (
                        var x in xs) n++;

                    while (c
                           && n > 0) n--;

                    if (
                        c) n++;
                    else n--;
                }
            }
            """
        );

    /// <summary>
    ///     The header's last line is 120 columns without the statement and 125 with it: the oracle chops
    ///     the condition and keeps <c>n++</c> after the <c>)</c>, so the condition was resolved against a
    ///     line that still held the statement — <see cref="GapRule.LastResortPoint" />.
    /// </summary>
    [Fact]
    public void TheHeaderWrapsBeforeTheStatementMoves() =>
        Agrees(
            """
            class T {
                void M(bool c, int n, int[] xs) {
                    while (
                        c && n > 0 && xs.Length > 0 && xs[0] > 0 && xs[1] > 0 && xs[2] > 0 && xs[3] > 0 && xs[4] > 0 && n < 1000000) n++;
                }
            }
            """,
            """
            class T {
                void M(bool c, int n, int[] xs) {
                    while (
                        c
                        && n > 0
                        && xs.Length > 0
                        && xs[0] > 0
                        && xs[1] > 0
                        && xs[2] > 0
                        && xs[3] > 0
                        && xs[4] > 0
                        && n < 1000000) n++;
                }
            }
            """
        );

    [Fact]
    public void AStatementWithNoRoom_StillMoves_AndTheAuthorsOwnBreakIsKept() =>
        Agrees(
            """
            class T {
                void M(bool c, int depth) {
                    if (depth < 0) throw new System.InvalidOperationException("a message long enough to run the whole line past the margin");

                    while (c)
                        depth++;

                    while (c) depth++;
                }
            }
            """,
            """
            class T {
                void M(bool c, int depth) {
                    if (depth < 0)
                        throw new System.InvalidOperationException("a message long enough to run the whole line past the margin");

                    while (c)
                        depth++;

                    while (c) depth++;
                }
            }
            """
        );
}

/// <summary>
///     SK-DIV-0112: a call chain headed by a parenthesised expression or a tuple spends no level of its
///     own once its owner has spent one — under an arrow, an <c>=</c> or a <c>return</c> the dots land
///     on the parenthesis's column — and still takes one as a statement of its own.
///     <c>constructs/breaks/chain-after-parenthesised-head.cs</c>.
/// </summary>
public sealed class ChainAfterParenthesisedHeadTests {
    /// <summary>One point before <c>.B</c> only, so the chain has no group and the frame decides.</summary>
    [Fact]
    public void AfterTheArrow_TheDotsTakeTheParenthesisColumn() =>
        Oracle.Agrees(
            """
            class T {
                object A() =>
                    (
                        a).B
                    .C();

                object B() =>
                    (a
                        + b).C
                    .D();

                object a, b;
            }
            """,
            """
            class T {
                object A() =>
                    (
                        a).B
                    .C();

                object B() =>
                    (a
                        + b).C
                    .D();

                object a, b;
            }
            """
        );

    /// <summary>
    ///     Two invoked dots, so the chain has a group of its own and the group decides; a property run
    ///     broken at every dot, which the frame decides; and a <c>?.</c> whose dot break hangs off
    ///     WhenNotNull, which the arrow's own walk has to see.
    /// </summary>
    [Fact]
    public void WithAGroup_AndThroughAConditionalAccess_TheSame() =>
        Oracle.Agrees(
            """
            class T {
                object N() =>
                    (
                        a).B()
                    .C();

                object A() =>
                    (
                        a)
                    .B
                    .C();

                object E() =>
                    (
                        a)?.B
                    .C();

                object a;
            }
            """,
            """
            class T {
                object N() =>
                    (
                        a).B()
                    .C();

                object A() =>
                    (
                        a)
                    .B
                    .C();

                object E() =>
                    (
                        a)?.B
                    .C();

                object a;
            }
            """
        );

    [Fact]
    public void AsAStatement_TheChainStillTakesTheStatementsLevel() =>
        Oracle.Agrees(
            """
            class T {
                object M() {
                    (a + b).C
                    .D();
                    (a + b).C()
                    .D()
                    .E();
                    var v = a.B
                        .C();
                    return v;
                }

                object a, b;
            }
            """,
            """
            class T {
                object M() {
                    (a + b).C
                        .D();
                    (a + b).C()
                        .D()
                        .E();
                    var v = a.B
                        .C();
                    return v;
                }

                object a, b;
            }
            """
        );
}

/// <summary>
///     SK-DIV-0109, the binary half of SK-DIV-0007: an item made multi-line by a break the author kept
///     inside it chops the list around it, and the same containment reaches an outer binary operator,
///     a ternary and an initializer — while a chain-wide owner is not chopped by its own links.
///     <c>constructs/breaks/multiline-item-chops-the-list.cs</c>.
/// </summary>
public sealed class MultilineItemChopsTheListTests {
    [Fact]
    public void AKeptOperatorBreakInsideAnItem_ChopsTheList() =>
        Oracle.Agrees(
            """
            class T {
                void C(int a = 5
                    + 6) { }

                void M() {
                    F(1
                        + 2, 3);
                    F(x => x
                        + 1, 3);
                }

                void F(object a, int b) { }
            }
            """,
            """
            class T {
                void C(
                    int a = 5
                        + 6
                ) { }

                void M() {
                    F(
                        1
                        + 2,
                        3
                    );
                    F(
                        x => x
                            + 1,
                        3
                    );
                }

                void F(object a, int b) { }
            }
            """
        );

    /// <summary>
    ///     Broken at the inner <c>&amp;&amp;</c>, the outer <c>||</c> chops too; broken at the outer
    ///     <c>||</c>, the inner stays — the chain-wide owner does not read its own links' breaks.
    /// </summary>
    [Fact]
    public void AnOuterOperator_ChopsAroundABrokenInnerOne_AndNotTheReverse() =>
        Oracle.Agrees(
            """
            class T {
                void M(bool c, int n) {
                    var z = a > 0
                        && b > 0 || c;
                    var q = a > 0 && b > 0
                        || c;
                    var r = c ? 1
                        + n : 2;
                }

                int a, b;
            }
            """,
            """
            class T {
                void M(bool c, int n) {
                    var z = a > 0
                        && b > 0
                        || c;
                    var q = a > 0 && b > 0
                        || c;
                    var r = c
                        ? 1
                        + n
                        : 2;
                }

                int a, b;
            }
            """
        );
}

/// <summary>
///     SK-DIV-0107: a switch expression's arms take one level from the line its governing expression
///     starts on, not from the line its <c>{</c> lands on.
///     <c>constructs/breaks/switch-over-multiline-governing-expression.cs</c>.
/// </summary>
public sealed class SwitchOverMultilineGoverningExpressionTests {
    [Fact]
    public void TheArmsNestFromTheGoverningExpressionsLine() =>
        Oracle.Agrees(
            """
            class T {
                int A(int a, int b) {
                    var s = (a,
                        b) switch {
                        (1, _) => 1,
                        _ => 2
                    };
                    int t = a.ToString()
                        .Length switch {
                        1 => 1,
                        _ => 2
                    };
                    return s + t;
                }
            }
            """,
            """
            class T {
                int A(int a, int b) {
                    var s = (a,
                        b) switch {
                        (1, _) => 1,
                        _ => 2
                    };
                    int t = a.ToString()
                        .Length switch {
                        1 => 1,
                        _ => 2
                    };
                    return s + t;
                }
            }
            """
        );

    [Fact]
    public void UnderAnArrow_AndMidLine_TheSameLineIsTheAnchor() =>
        Oracle.Agrees(
            """
            class T {
                int B(int a, int b) => (a,
                    b) switch {
                    (1, _) => 1,
                    _ => 2
                };

                int D(int a, int b) {
                    var s = a + (a,
                        b) switch {
                        (1, _) => 1,
                        _ => 2
                    };
                    return s;
                }
            }
            """,
            """
            class T {
                int B(int a, int b) =>
                    (a,
                        b) switch {
                        (1, _) => 1,
                        _ => 2
                    };

                int D(int a, int b) {
                    var s = a
                        + (a,
                            b) switch {
                            (1, _) => 1,
                            _ => 2
                        };
                    return s;
                }
            }
            """
        );
}

/// <summary>
///     SK-DIV-0110: a fill breaks before an item only when that makes the item fit whole, so a nested
///     tuple carrying a kept break of its own keeps its head after the comma.
///     <c>constructs/breaks/nested-multiline-tuple-item.cs</c>.
/// </summary>
public sealed class NestedMultilineTupleItemTests {
    [Fact]
    public void ANestedItemWithAKeptBreak_KeepsItsHeadAfterTheComma() =>
        Oracle.Agrees(
            """
            class T {
                void M() {
                    var t = (1
                        , (2
                            , 3));
                    var f = (1
                        , (2
                            , 3), 4);
                }
            }
            """,
            """
            class T {
                void M() {
                    var t = (1
                        , (2
                            , 3));
                    var f = (1
                        , (2
                            , 3), 4);
                }
            }
            """
        );
}

/// <summary>
///     SK-DIV-0111: a <c>for</c> header is multi-line when a break inside its parentheses
///     <em>survives</em> the constructs inside it, not when the source merely holds one — so a break the
///     declarators, a binary operator or an invocation re-join leaves the header whole.
///     <c>constructs/breaks/for-header-surviving-break.cs</c>.
/// </summary>
public sealed class ForHeaderSurvivingBreakTests {
    [Fact]
    public void ABreakTheDeclaratorsReJoin_LeavesTheHeaderWhole() =>
        Oracle.Agrees(
            """
            class T {
                void M(int n) {
                    for (int i = 0
                        , j = 1; i < n; i++) { }

                    for (int i = 0; i <
                        n; i++) { }

                    for (int i = F(
                        1); i < n; i++) { }
                }

                int F(int a) => a;
            }
            """,
            """
            class T {
                void M(int n) {
                    for (int i = 0, j = 1; i < n; i++) { }

                    for (int i = 0; i < n; i++) { }

                    for (int i = F(1); i < n; i++) { }
                }

                int F(int a) => a;
            }
            """
        );

    [Fact]
    public void ABreakThatIsKept_StillChopsIt() =>
        Oracle.Agrees(
            """
            class T {
                void M(int n) {
                    for (int i = 0,
                        j = 1; i < n; i++) { }

                    for (int i = 0, j = 1; i < n
                        && j > 0; i++) { }
                }
            }
            """,
            """
            class T {
                void M(int n) {
                    for (int i = 0,
                         j = 1;
                         i < n;
                         i++) { }

                    for (int i = 0, j = 1;
                         i < n
                         && j > 0;
                         i++) { }
                }
            }
            """
        );
}
