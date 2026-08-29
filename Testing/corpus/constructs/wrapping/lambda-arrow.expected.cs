// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
using System;
using System.Collections.Generic;

// A lambda's `=>` is a break point of the oracle's, and Skala has none there — see SK-DIV-0050,
// which is the shape this file does *not* contain. What it pins is everything around that gap:
// where the oracle declines the arrow break, what it does with the author's own, and that the five
// `resharper_*lambda_and_anonymous_function*` wrapping keys move none of it.
class LambdaArrow {
    void M() {
        // The oracle declines the arrow here and chops the body's argument list instead, and so
        // does Skala. ⚠ Not a rule: `Action<int> a = value => Call(a, b, c, d);` of the same width
        // comes back from the oracle with the arrow broken, which is SK-DIV-0050's pair.
        Action first = () => DoSomethingWithARatherLongName(firstArgument, secondArgument, thirdArgument, fourthArgN);

        // A break the author wrote after the arrow survives — `keep_user_linebreaks` governs it,
        // not `keep_existing_expr_member_arrangement`, which is the guess the expression-body plan
        // invites. Measured: it survives with the expression-member key flipped and re-joins under
        // `keep_user_linebreaks = false` and `keep_existing_linebreaks = false` alike.
        Func<int, int> second = value =>
            value + 1;

        // A lambda short enough to stay whole keeps its line.
        Func<int, int> third = value => value + 1;

        // ⚠ The sole argument of a call: `place_single_method_argument_lambda_on_same_line = true`
        // pins the lambda to the call's line and the oracle chops the body rather than the arrow.
        Throws<ArgumentException>(() => Simulate(volumeArgument, groundArgument, fieldArgument, extraArgumentName));

        // A parenthesized lambda's parameter list is governed by the *declaration* keys —
        // `wrap_parameters_style` and `max_formal_parameters_on_line` — and by none of the five
        // named for lambdas. Measured at 120 and at 60 columns, on this shape and on the
        // `delegate(…)` one below.
        Use((
                SomeVeryLongParameterTypeName firstParameterName,
                AnotherLongParameterTypeName secondParameterName,
                ThirdParameterTypeName thirdParameterName
            ) => firstParameterName
        );

        Use(delegate(SomeVeryLongParameterTypeName firstParameterName, AnotherLongParameterTypeName sec) { return 1; });
    }

    void Throws<T>(Action action) { }
    void Use(object o) { }
    void Simulate(object a, object b, object c, object d) { }
    void DoSomethingWithARatherLongName(object a, object b, object c, object d) { }
}
