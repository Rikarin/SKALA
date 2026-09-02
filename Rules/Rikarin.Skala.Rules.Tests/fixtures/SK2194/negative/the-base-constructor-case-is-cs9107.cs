// The compiler already warns here, always on and with no analyzer package: CS9107, "captured into
// the state of the enclosing type and its value is also passed to the base constructor". Probed
// on a real build rather than assumed. Restating it would be a double-count.
namespace Fixtures {
    class Origin(int value) {
        public int Value => value;
    }

    sealed class Shifted(int value) : Origin(value) {
        public void Bump() => value++;
    }
}
