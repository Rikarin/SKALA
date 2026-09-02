using System.Collections.Generic;

namespace Fixtures {
    sealed class Counter {
        public int Value;

        public void Increment() => Value++;
    }

    sealed class Runner {
        public static void BumpAll(List<Counter> counters) {
            foreach (var counter in counters) {
                counter.Increment();
            }
        }
    }
}
