// `!` on a non-nullable value type has never suppressed anything in any context.
namespace Fixtures {
    sealed class Counter {
        public int Next(int seed) {
            var value = seed!;
            return value + 1;
        }
    }
}
