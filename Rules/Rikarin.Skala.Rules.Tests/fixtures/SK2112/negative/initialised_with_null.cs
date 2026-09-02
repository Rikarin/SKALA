// The annotation is telling the truth.
namespace Fixtures {
    sealed class Empty {
        public int Measure() {
            string? name = null;
            return name?.Length ?? 0;
        }
    }
}
