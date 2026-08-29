// SK-DIV-0078, on its own file, and no option is globbed to it.
//
// ⚠ This fixture is expected to disagree with the oracle and exists to hold the disagreement still.
// The author broke the pattern chain before each `or`, `wrap_before_binary_pattern_op = true` keeps
// those breaks, and the oracle then also breaks the expression body's `=>` —
//     bool M(object o) =>
//         o is int
//             or string
// — because `place_expr_method_on_single_line = if_owner_is_single_line` reads the *declaration* as
// not single-line once the body spans lines. Skala's fitter resolves the arrow's group before the
// chain's, sees a first line 33 columns wide, and leaves the arrow flat. The one-line member beside
// it is the control.
class BinaryPatternArrow {
    bool M(object o) => o is int
        or string
        or bool;

    // The control: nothing to break, and the arrow stays where it is on both sides.
    bool Short(object o) => o is int or string;
}
