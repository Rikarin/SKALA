// ⚠ This file satisfies both shapes, which is the only way to test disjointness. `a & b == c` parses
// as `a & (b == c)` and only compiles when every operand is `bool` — which is exactly SK2064's
// subject. SK2064 reports it and offers `&&`; this rule declines it, so the token is reported once.
// Arithmetic and shift operands are never `bool`, so the two rules cannot both fire on any
// expression.
class C {
    bool And(bool a, bool b, bool c) => a & b == c;

    bool Or(bool a, bool b, bool c) => a | b != c;
}
