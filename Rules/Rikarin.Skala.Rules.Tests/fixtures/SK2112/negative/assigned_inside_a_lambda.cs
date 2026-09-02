// ⚠ An assignment written inside a lambda is still this method's assignment, which is why the
// walk covers the whole member rather than stopping at the nested function.
namespace Fixtures {
    sealed class Captured {
        public int Measure() {
            string? name = "a";
            System.Action clear = () => name = null;
            clear();
            return name?.Length ?? 0;
        }
    }
}
