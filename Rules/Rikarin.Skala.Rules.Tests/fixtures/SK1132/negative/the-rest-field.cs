// ⚠ `t.Rest.Item1` is the eighth element reached explicitly, and `Rest`'s type is `ValueTuple<int, int>`,
// which has no names. Only `t.Item8` carries the name `H`.
public static class Wide {
    public static int Eighth() {
        var t = (A: 1, B: 2, C: 3, D: 4, E: 5, F: 6, G: 7, H: 8, I: 9);
        return t.Rest.Item1 + t.H;
    }
}
