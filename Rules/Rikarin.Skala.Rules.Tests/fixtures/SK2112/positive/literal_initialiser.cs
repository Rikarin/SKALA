// The `?` is a null check every following line has to justify, and there is nothing to justify.
namespace Fixtures {
    sealed class Greeter {
        public int Measure() {
            string? name = "anonymous";
            return name.Length;
        }
    }
}
