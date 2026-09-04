// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
// resharper_csharp_wrap_before_binary_pattern_op = true: the break belongs before the operator, so a
// break the author wrote *before* one is kept and one written *after* is re-joined. Both directions,
// because one alone cannot tell "the break is before the operator" from "the break is preserved".
//
// ⚠ Statements and not expression bodies, and that is SK-DIV-0078 rather than taste: once the chain
// is broken the oracle also breaks an expression body's `=>`, because
// `place_expr_method_on_single_line = if_owner_is_single_line` reads the declaration as not
// single-line as soon as the body spans lines, and Skala's fitter resolves the arrow's group before
// the chain's and cannot see it. While this file was written with `=>` members it disagreed with the
// oracle at the sweep's baseline and this key's row attributed nothing. The arrow case is pinned on
// wrapping/binary-pattern-arrow.cs.

class BinaryPatterns {
    bool Before(object o) {
        return o is int
            or string
            or bool;
    }

    bool After(object o) {
        return o is int or string or bool;
    }
}
