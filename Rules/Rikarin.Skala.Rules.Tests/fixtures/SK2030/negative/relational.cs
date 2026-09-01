// Relational comparisons with NaN are also constant, and no single IsNaN call replaces them.
class C {
    bool M(double x) => x < double.NaN;

    bool N(double x) => x >= double.NaN;
}
