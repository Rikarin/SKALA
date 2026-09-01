// ⚠ The exclusion that makes this rule semantic. `x - x` on a floating-point value is 0 for every
// finite x and NaN for NaN and the infinities — a real technique for propagating NaN through an
// expression, and textually identical to the defect.
class C {
    double M(double x) => x - x;

    float N(float x) => x - x;
}
