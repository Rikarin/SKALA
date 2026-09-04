// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
// resharper_keep_existing_lambda_and_anonymous_function_parens_arrangement = true, with
// wrap_after_declaration_lpar and wrap_before_declaration_rpar reading the same parentheses.
//
// ⚠ The `delegate(…) { … }` case moved to anonymous-method-parens.cs, and it is SK-DIV-0077 rather
// than a tidy-up: an anonymous method whose parameter list the author broke leaves the call's line
// and has its block body broken with it, and Skala does neither. While it sat here it made this file
// disagree with the oracle at the sweep's baseline, and all three of its keys attributed nothing.

class LambdaParens {
    void M() {
        Use((int first) => first);
        Use((
                int first,
                int second
            ) => first
        );
    }
}
