// Merging these means renaming an identifier inside a body, and a rename is a rewrite this fix has
// no way to bound — the name may be shadowed, captured, or spelled in a `nameof`.
namespace Fixtures {
    static class DifferingNames {
        public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);

        public static int Size(this string s) => s.Length;
    }
}
