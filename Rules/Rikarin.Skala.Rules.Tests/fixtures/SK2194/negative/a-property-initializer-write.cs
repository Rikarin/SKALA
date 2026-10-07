// A property initializer is construction, like a field initializer: `readonly struct` accepts a write
// there, and the value it leaves is set before anything can observe it.
namespace Fixtures {
    struct Seeded(int seed) {
        public int First { get; } = seed++;

        public int Seed => seed;
    }
}
