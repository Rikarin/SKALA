// ⚠ The compiler already owns this one, and it is measured rather than assumed: `CS0251`,
// "indexing an array with a negative index", is reported here and is silent on `"abc"[-1]`,
// `list[-1]` and `span[-1]`, which all throw at run time just the same. Reporting it again would be
// two diagnostics on one bracket.
#pragma warning disable CS0251
class C {
    int Before(int[] values) => values[-1];
}
#pragma warning restore CS0251
