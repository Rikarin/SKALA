// The coalescing operator is what makes the flow state NotNull, and it is what the annotation
// should have been updated to reflect.
namespace Fixtures {
    sealed class Resolver {
        static string? Source() => null;

        public int Measure() {
            string? name = Source() ?? "fallback";
            return name.Length;
        }
    }
}
