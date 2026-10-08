// A sole lambda argument broken before `=>`, with a block body, puts the arrow two levels past the line
// the call starts on — the argument list's unconditional level and the arrow's own — and the block one
// level past it, `}` on the argument level (#488, SK-DIV-0169). Under
// `place_single_method_argument_lambda_on_same_line = true`, the export's value.
//
// ⚠ With an expression body the arrow takes one level, not two; without the call (`_f = (int first)` /
// `=> {`) it takes one too. A named or non-sole lambda argument is laid out like any argument.
class SoleLambdaArgumentArrow {
    void M() {
        Use((int first)
            => {
                return first;
            }
        );
        Use(first
            => {
                return first;
            }
        );
        Use(async (int first)
            => {
                return first;
            }
        );
        var u = Use((int first)
            => {
                return first;
            }
        );
        Outer(Use((int first)
            => {
                return first;
            }
        ));
        Use((int first)
            => first + 1
        );
        Use(a, (int first)
            => {
                return first;
            }
        );
        _f = (int first)
            => {
                return first;
            };
    }
}
