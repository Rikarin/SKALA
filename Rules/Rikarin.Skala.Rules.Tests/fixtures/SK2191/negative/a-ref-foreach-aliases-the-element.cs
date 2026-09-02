using System;

namespace Fixtures {
    struct Counter {
        public int Value;

        public void Increment() => Value++;
    }

    sealed class Runner {
        public static void BumpAll(Span<Counter> counters) {
            foreach (ref var counter in counters) {
                counter.Increment();
            }
        }
    }
}
