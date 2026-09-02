// ⚠ `!(left < right)` is `left >= right`, which is a different rewrite and not this concept —
// and it is wrong for floating point, where neither comparison holds against a NaN.
class C {
    public static bool Run(int left, int right) => !(left < right);
}
