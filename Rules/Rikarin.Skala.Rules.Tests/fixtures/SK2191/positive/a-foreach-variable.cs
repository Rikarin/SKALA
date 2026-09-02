namespace Fixtures {
    struct Counter {
        public int Value;

        public void Increment() => Value++;
    }

    sealed class Runner {
        public static void BumpAll(Counter[] counters) {
            foreach (var counter in counters) {
                counter.Increment();
            }
        }
    }
}
