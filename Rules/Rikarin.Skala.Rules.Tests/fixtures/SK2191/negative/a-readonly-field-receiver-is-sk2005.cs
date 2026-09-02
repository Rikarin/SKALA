// The same lost write through a `readonly` field. SK2005 reports it; this rule must not, or the
// two would argue over one span every time either is turned on.
namespace Fixtures {
    struct Counter {
        public int Value;

        public void Increment() => Value++;
    }

    sealed class Runner {
        readonly Counter counter;

        static readonly Counter Shared;

        public void Bump() => counter.Increment();

        public static void BumpShared() => Shared.Increment();
    }
}
