// An `init` accessor is the compiler's own exception: `readonly struct` accepts a write there,
// because it can only run while the instance is being initialized.
namespace Fixtures {
    struct Sized(int size) {
        public int Size {
            get => size;
            init => size = value;
        }
    }
}
