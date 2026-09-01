// The operand is not floating point before the comparison widens it. `double.IsNaN(x)` would be
// just as constantly false as this is, so the rewrite would change nothing.
class C {
    bool M(int x) => x == double.NaN;
}
