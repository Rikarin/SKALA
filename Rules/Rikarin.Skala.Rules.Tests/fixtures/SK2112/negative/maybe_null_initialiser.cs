// The initialiser's flow state is MaybeNull, so nothing is proved.
namespace Fixtures {
    sealed class Uncertain {
        static string? Source() => null;

        public int Measure() {
            string? name = Source();
            return name?.Length ?? 0;
        }
    }
}
