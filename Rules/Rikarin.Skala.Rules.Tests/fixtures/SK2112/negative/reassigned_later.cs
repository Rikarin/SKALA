// A single assignment is what makes the claim provable; a second one can put null back.
namespace Fixtures {
    sealed class Rewritten {
        static string? Source() => null;

        public int Measure() {
            string? name = "a";
            name = Source();
            return name?.Length ?? 0;
        }
    }
}
