// A tuple is a struct: writing a named element writes the capture (CS9117).
namespace Fixtures {
    sealed class Range((int Low, int High) bounds) {
        public void Widen(int by) => bounds.High += by;

        public int Width => bounds.High - bounds.Low;
    }
}
