using System.Diagnostics.Contracts;

// ⚠ The results leave through the parameter. Code Contracts explicitly permitted this shape, and
// "returns nothing" is not true of it in the sense the rule means.
static class Parsing {
    [Pure]
    public static void Split(string input, out int length) => length = input.Length;
}
