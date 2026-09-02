// There is no `?` to remove; `var` carries the annotation the initialiser had.
namespace Fixtures {
    sealed class Inferred {
        public int Measure() {
            var name = "a";
            return name.Length;
        }
    }
}
