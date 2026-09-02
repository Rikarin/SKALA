using System.Diagnostics;

namespace Fixtures {
    struct Counter {
        public int Value;

        [Conditional("DEBUG")]
        public void Increment() => Value++;
    }

    sealed class Runner {
        public static void Bump(in Counter counter) => counter.Increment();
    }
}
