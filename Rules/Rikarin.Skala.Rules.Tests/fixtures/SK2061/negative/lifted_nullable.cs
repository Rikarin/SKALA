// A lifted operation over nullable operands has its own answer for null, which is not the one the
// rule assumes.
class C {
    bool M(int? x) => x == x;

    bool N(int? x) => x < x;
}
