// ⚠ The same defect one assignment away, and deliberately not followed: proving the field holds
// NaN needs its initialiser and the absence of every other writer.
class C {
    static readonly double Sentinel = double.NaN;

    bool M(double x) => x == Sentinel;
}
