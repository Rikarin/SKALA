// ⚠ Removing this `?` changes the type rather than an annotation, and breaks `HasValue`, boxing
// and comparison against null.
namespace Fixtures {
    sealed class Counted {
        public int Measure() {
            int? count = 1;
            return count.Value;
        }
    }
}
