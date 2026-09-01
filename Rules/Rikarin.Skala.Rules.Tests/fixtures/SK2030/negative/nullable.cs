// A lifted comparison answers differently for the null operand, so `double.IsNaN` is not the
// same program. The rule reports only comparisons it can rewrite.
class C {
    bool M(double? x) => x == double.NaN;
}
