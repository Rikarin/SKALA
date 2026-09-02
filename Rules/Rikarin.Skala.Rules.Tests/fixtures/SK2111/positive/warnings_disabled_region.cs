// ⚠ Warnings and annotations are switched separately. Here the `?` still means something and the
// diagnostics are off, so the suppression is still inert.
#nullable enable
namespace Fixtures {
    sealed class Scanner {
#nullable disable warnings
        public int Measure(string? text) => text!.Length;
#nullable restore warnings
    }
}
