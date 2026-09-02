// ⚠ #302's shape, in the one place it takes a real `///` (#325). A local declaration with no
// modifiers begins at its type, so the doc comment above it is leading trivia of the `string?` node
// the guard was asked about — and the fix deletes a single `?` token, which no comment can reach.
namespace Fixtures {
    sealed class Greeter {
        public int Measure() {
            /// <summary>The name shown when nobody supplied one.</summary>
            string? name = "anonymous";
            return name.Length;
        }
    }
}
