// ⚠ The idiomatic NaN test written without a call. `x == x` is false exactly when x is NaN, and
// `x != x` is the same test negated. Textually identical to the defect; entirely deliberate.
class C {
    bool IsNan(double x) => x != x;

    bool IsNumber(double x) => x == x;
}
