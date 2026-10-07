// A capture on the right of a deconstruction is read, and `var (a, b)` declares new locals.
namespace Fixtures {
    sealed class Extent(int start, int end) {
        public int Length() {
            var (from, to) = (start, end);
            int low;
            int high;
            (low, high) = (start, end);
            return to - from + high - low;
        }
    }
}
