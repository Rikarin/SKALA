// ⚠ The callee decides the value, and the parameter's own annotation is what the call site has
// to match.
namespace Fixtures {
    sealed class Filled {
        static void Fill(ref string? slot) => slot = null;

        public int Measure() {
            string? name = "a";
            Fill(ref name);
            return name?.Length ?? 0;
        }
    }
}
