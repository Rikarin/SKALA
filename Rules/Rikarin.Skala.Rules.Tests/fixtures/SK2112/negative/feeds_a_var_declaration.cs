// ⚠ The cascade guard. Removing the `?` changes what `var copy` infers, and the assignment two
// lines down would become a warning the fix caused.
namespace Fixtures {
    sealed class Cascading {
        public int Measure() {
            string? name = "a";
            var copy = name;
            copy = null;
            return copy?.Length ?? 0;
        }
    }
}
