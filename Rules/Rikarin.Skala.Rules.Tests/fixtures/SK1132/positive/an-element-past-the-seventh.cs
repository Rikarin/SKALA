// ⚠ `Item8` is stored in `Rest.Item1`, and the semantic model hides that: `t.Item8` binds to a field of
// the nine-element tuple type itself, whose flat element list names it `H`. The rename is `t.H`, never a
// name taken from the wrong position.
public static class Wide {
    public static int Eighth() {
        var t = (A: 1, B: 2, C: 3, D: 4, E: 5, F: 6, G: 7, H: 8, I: 9);
        return t.Item8 + t.Item9;
    }
}
