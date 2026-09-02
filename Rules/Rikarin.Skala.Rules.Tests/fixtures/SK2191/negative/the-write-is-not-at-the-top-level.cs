// The evidence bar is a write the analysis has read at the top level of the body. A write nested
// inside a condition or a loop is not claimed, because "this method mutates" would then be a
// guess about the path taken.
namespace Fixtures {
    struct Counter {
        public int Value;

        public void MaybeIncrement(bool when) {
            if (when) {
                Value++;
            }
        }
    }

    sealed class Runner {
        public static void Bump(in Counter counter) => counter.MaybeIncrement(true);
    }
}
