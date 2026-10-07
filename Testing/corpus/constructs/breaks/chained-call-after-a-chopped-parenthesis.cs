namespace Constructs.Breaks;

// SK-DIV-0156 (issue #404). A body opening with a parenthesis the author broke after puts the `(`
// at the owner's indent (SK-DIV-0101) only while the chain after the `)` stays whole. Two calls or
// more — an indexer counts as one — is a chain that breaks at its dots whenever its head spans
// lines, and then the `(` stays on the arrow's, the `=`'s or the `return`'s continuation, exactly
// as for a chain the author broke. Skala read the author's break off the source, so it held the
// level on pass one, broke the chain, and gave the level up on pass two. One call, an indexer
// alone, a property run and a `?.` call break nothing and still hold.
public class ChainedCallAfterAChoppedParenthesis {
    object AnIndexerAndACall() =>
        (
            a)[0].C();

    object TwoCalls =>
        (
            a).B().C();

    object AConditionalCall() =>
        (
            a)[0]?.C();

    object ASuppressedHead() =>
        (
            a ?? b)!.B().C();

    object ATupleHead() =>
        (
            a, b).B().C().D();

    object ATernaryCondition() =>
        (
            a)[0].C() ? a : b;

    object ABinaryOperand() =>
        (
            a).B().C() + b;

    void UnderAnEqualsAndALambda() {
        var x =
            (
                a)[0].C();
        System.Func<object> f = () =>
            (
                a).B().C();
    }

    object AfterAReturn() {
        return
            (
                a)[0].C();
    }

    object OneCallHolds() =>
        (
            a).C();

    object AnIndexerAloneHolds() =>
        (
            a)[0];

    object APropertyRunHolds() =>
        (
            a).B.C();

    object AConditionalCallAloneHolds() =>
        (
            a)?.B();

    object a, b;
}
