// Infinity, unlike NaN, compares equal to itself. This comparison works.
class C {
    bool M(double x) => x == double.PositiveInfinity;
}
