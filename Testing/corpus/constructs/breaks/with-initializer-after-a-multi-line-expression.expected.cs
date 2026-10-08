// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A `with` initializer breaks open whenever the expression before `with` spans lines — an argument list
// the author broke, one too long for the line, a broken operand, a broken chain — and its members nest
// from the line the `with` expression starts on (#487, SK-DIV-0168).
//
// ⚠ An object creation's initializer in the same place stays whole, and so does a `with` that is one
// line itself, inside a chopped argument list or after a lambda whose block re-joins.

class WithInitializerAfterAMultiLineExpression {
    void M() {
        _r = Make(
            alphaArgumentValueNumberOneLonger,
            betaArgumentValueNumberTwoLonger,
            gammaArgumentValueThree
        ) with {
            P = 2
        };
        _r = Make(alphaArgumentValueNumberOneLonger, betaArgumentValueNumberTwoLonger, gammaArgumentValue) with {
            P = 2
        };
        _r = Make(
            alpha,
            beta
        ) with {
            P = 2,
            Q = 3,
            R = 4,
            S = 5,
            T = 6
        };
        _r = Make(
            alpha,
            beta
        ) with { };
        _r = (alphaArgumentValueNumberOneLonger
            + betaArgumentValueNumberTwoLonger) with {
            P = 2
        };
        _r = someReceiver.FirstCall()
            .SecondCall() with {
            P = 2
        };
        _r = Make(
            alpha,
            beta
        ) with {
            P = Make(
                gamma,
                delta
            )
        };
        _r = Make(() => { A(); }) with { P = 2 };
        Use(
            _r with { P = 2 },
            b
        );
        var q = new R(
            alpha,
            beta
        ) { P = 2 };
    }
}
