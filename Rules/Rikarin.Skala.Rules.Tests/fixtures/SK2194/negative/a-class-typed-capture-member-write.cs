// #408's own suggested negative: a class-typed capture's field is a field of another object. The
// capture still points at the same instance, and `readonly struct` accepts the write.
namespace Fixtures {
    sealed class Tally {
        public int Count;
    }

    struct Recorder(Tally tally) {
        public void Record() => tally.Count++;

        public void Clear() => Reset(ref tally.Count);

        public int Count => tally.Count;

        static void Reset(ref int target) => target = 0;
    }
}
