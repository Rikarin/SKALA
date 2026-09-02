// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
// SK-DIV-0077's anonymous-method half, on its own file, and no option is globbed to it.
//
// ⚠ This fixture is expected to disagree with the oracle and exists to hold the disagreement still.
// The author broke the parameter list, `keep_existing_lambda_and_anonymous_function_parens_arrangement
// = true` preserves that break, and the oracle then does two more things Skala does not: it moves the
// whole anonymous method off the call's line, and it breaks the block body that the author wrote on
// one line. The lambda beside it is the control — there the two agree, and `Use((` stays joined —
// which is what says the divergence is the anonymous method rather than the broken parentheses.

class AnonymousMethodParens {
    void M() {
        Use(
            delegate(
                int first
            ) {
                return first;
            }
        );

        // The control: a lambda whose parentheses the author broke the same way.
        Use((
                int first
            ) => first
        );
    }
}
