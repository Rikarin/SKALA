// #408: both elements of a deconstruction are written. `readonly struct` makes each one CS9114.
namespace Fixtures {
    struct Span2(int start, int end) {
        public void Swap() => (start, end) = (end, start);

        public int Length => end - start;
    }
}
