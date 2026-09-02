// ⚠ `x[..^0]` is the whole collection and `x[^0..]` is an empty slice. Both are legal, and both
// are legal on an empty collection too — measured, not assumed. `^0` is only wrong where it fetches
// an element, so the syntactic parent is what decides: an index argument is reported, a range
// endpoint is not.
class C {
    int[] Whole(int[] values) => values[..^0];

    int[] Nothing(int[] values) => values[^0..];

    string WholeText(string text) => text[..^0];
}
