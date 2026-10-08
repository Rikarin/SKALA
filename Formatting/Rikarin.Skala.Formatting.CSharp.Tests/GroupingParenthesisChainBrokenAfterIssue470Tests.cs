namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #470, SK-DIV-0112, SK-DIV-0148, SK-DIV-0159: a grouping parenthesis heading a chain that breaks
///     before a dot after its <c>)</c> nests its contents from the chain's continuation line when that line is
///     deeper than the <c>(</c>'s. Every expected string is <c>jb cleanupcode</c>'s own output for the input,
///     and each is checked on a second pass; <c>constructs/syntax/grouping-parenthesis-chain-broken-after.cs</c>
///     holds the wider set.
/// </summary>
public sealed class GroupingParenthesisChainBrokenAfterIssue470Tests {
    /// <summary>
    ///     An author's break before a dot that is not a break point, paid by a frame: after an <c>=</c>, after
    ///     <c>return</c> and with a binary inside the parenthesis, the contents go two levels past the statement.
    /// </summary>
    [Fact]
    public void AFrameBrokenChain_LiftsTheParenthesisContents() =>
        Oracle.Agrees(
            """
            class C {
                object M() {
                    var z1 = (
            a).B
            .C();
                    var z2 = (a
            + b).C
            .D();
                    return (
            a).B
            .C();
                }
            }
            """,
            """
            class C {
                object M() {
                    var z1 = (
                            a).B
                        .C();
                    var z2 = (a
                            + b).C
                        .D();
                    return (
                            a).B
                        .C();
                }
            }
            """
        );

    /// <summary>
    ///     A block on the parenthesis's own line nests from the chain's continuation line, and its <c>}</c> sits on
    ///     it; a chain group's switch arm (<c>=&gt; (</c>) lifts the same way.
    /// </summary>
    [Fact]
    public void ABlockAndAnArm_NestFromTheChainsContinuationLine() =>
        Oracle.Agrees(
            """
            class C {
                void M() {
                    var x = (y switch {
            1 => 2,
            _ => 3
            }).ToString()
            .Length;
                    var s = q switch {
            1 => (
            a).B().C(),
            _ => 0
            };
                }
            }
            """,
            """
            class C {
                void M() {
                    var x = (y switch {
                            1 => 2,
                            _ => 3
                        }).ToString()
                        .Length;
                    var s = q switch {
                        1 => (
                                a).B()
                            .C(),
                        _ => 0
                    };
                }
            }
            """
        );

    /// <summary>
    ///     Where the break before the dot spends nothing — under an arrow that already broke, inside an argument
    ///     list — there is nothing to lift, on both sides.
    /// </summary>
    [Fact]
    public void WhereTheDotSpendsNothing_NothingIsLifted() =>
        Oracle.Agrees(
            """
            class C {
                object A() =>
            (
            a).B
            .C();

                void M() {
                    Call(
            (
            a).B
            .C());
                }
            }
            """,
            """
            class C {
                object A() =>
                    (
                        a).B
                    .C();

                void M() {
                    Call(
                        (
                            a).B
                        .C()
                    );
                }
            }
            """
        );
}
