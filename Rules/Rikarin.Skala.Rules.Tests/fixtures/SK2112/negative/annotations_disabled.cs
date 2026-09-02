// ⚠ The withdrawal fixture. Where annotations are off the flow state of every expression is
// `None` rather than `NotNull`, the `?` is already CS8632, and removing it is the compiler's
// finding rather than this rule's.
#nullable disable
namespace Fixtures {
    sealed class Oblivious {
        public int Measure() {
            string? name = "a";
            return name.Length;
        }
    }
}
