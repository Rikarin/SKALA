// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-16
// SK-DIV-0078, on its own file, and no option is globbed to it.
//
// ⚠ This fixture was expected to disagree with the oracle and existed to hold the disagreement
// still; it agrees since the containment fact landed with SK-DIV-0109 (issue #370). The author broke
// the pattern chain before each `or`, `wrap_before_binary_pattern_op = true` keeps those breaks, and
// the oracle then also breaks the expression body's `=>` —
//     bool M(object o) =>
//         o is int
//             or string
// — because `place_expr_method_on_single_line = if_owner_is_single_line` reads the *declaration* as
// not single-line once the body spans lines. Skala's fitter resolves the arrow's group before the
// chain's and used to see only a first line 33 columns wide; a group whose child is certain to
// break now has no flat form, so the arrow sees a body that spans lines. The one-line member beside
// it is the control.

class BinaryPatternArrow {
    bool M(object o) =>
        o is int
            or string
            or bool;

    // The control: nothing to break, and the arrow stays where it is on both sides.
    bool Short(object o) => o is int or string;
}
