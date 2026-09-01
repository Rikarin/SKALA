// A lifted operation over nullable operands has its own answer for null, which is not the one the
// rule assumes.
class C {
    int? M(int? x) => x - x;

    bool? N(bool? x) => x & x;
}
