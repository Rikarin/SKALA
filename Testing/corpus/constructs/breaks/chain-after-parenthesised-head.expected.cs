// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-16
namespace Constructs.Breaks;

// SK-DIV-0112 (issue #370). A call chain whose head is a parenthesised expression or a tuple spends
// no continuation level of its own once its owner has spent one: under an arrow, an `=` or a
// `return` that broke before the body, the dots land on the parenthesis's own column rather than
// one level past it. A single-line head, a tuple, a `?.` and a `[0]` after the `)` behave alike; an
// invocation head keeps the chain's level, and a chain that is a statement of its own — with no
// owner's level spent yet — still takes one. Before this file Skala put every such dot one level in.
public class ChainAfterParenthesisedHead {
    object AfterTheArrow() =>
        (
            a).B
        .C();

    object EveryDotBroken() =>
        (
            a)
        .B
        .C();

    object ALongerRun() =>
        (
            a).B.C()
        .D();

    object AnInvokedHead() =>
        (
            a).B()
        .C();

    object ASingleLineHead() =>
        (a + b).C
        .D();

    object AHeadBrokenInside() =>
        (a
            + b).C
        .D()
        .E();

    object ATupleHead() =>
        (
            a, b).C
        .D();

    object AConditionalAccess() =>
        (
            a)?.B
        .C();

    object AnElementAccess() =>
        (
            a)[0]
        .C();

    object TheArrowBreaksFirst() =>
        (
            a).B
        .C();

    object AfterAnEquals() {
        var x =
            (
                a).B
            .C();
        return x;
    }

    object AfterAReturn() {
        return
            (
                a).B
            .C();
    }

    object AsAStatementTheChainStillSpends() {
        (a + b).C
            .D();
        (a + b).C()
            .D()
            .E();
        return a;
    }

    object AnIdentifierHeadKeepsItsLevel() {
        var v = a.B
            .C();
        return v;
    }

    object a, b;
}
