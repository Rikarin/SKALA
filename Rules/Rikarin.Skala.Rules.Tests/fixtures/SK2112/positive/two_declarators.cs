// One `?` on the shared type, so the finding is about the declaration and both declarators have
// to qualify before the fix may remove it.
namespace Fixtures {
    sealed class Pair {
        public int Measure() {
            string? left = "l", right = "r";
            return left.Length + right.Length;
        }
    }
}
