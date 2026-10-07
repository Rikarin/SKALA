// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
// SK-DIV-0077's anonymous-method half, on its own file, and no option is globbed to it.
//
// The author broke the parameter list, `keep_existing_lambda_and_anonymous_function_parens_arrangement
// = true` preserves that break, and the oracle then does two more things: it moves the whole
// anonymous method off the call's line, and it breaks the block body that the author wrote on one
// line. The lambda beside it is the control — `Use((` stays joined — which is what says the first is
// the anonymous method rather than the broken parentheses. ⚠ This fixture held a disagreement still
// until #405: an anonymous method is not the single lambda argument that keeps the call's line
// (SK-DIV-0163), and a one-statement block breaks open when its owner's head spans lines.

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
