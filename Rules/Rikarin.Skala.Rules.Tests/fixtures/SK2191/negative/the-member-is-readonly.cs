// A `readonly` member cannot write, so the compiler makes no defensive copy for it at all.
namespace Fixtures {
    struct Counter {
        public int Value;

        public readonly void Touch() { }
    }

    sealed class Runner {
        public static void Show(in Counter counter) => counter.Touch();
    }
}
