// Writing an element of a captured array writes the array, never the capture, which still holds the
// same reference. `readonly struct` accepts it.
namespace Fixtures {
    struct Buffer(int[] items) {
        public void Put(int index, int value) => items[index] = value;

        public int Get(int index) => items[index];
    }
}
