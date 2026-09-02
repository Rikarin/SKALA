namespace Fixtures {
    struct Counter {
        public int Value;

        public void Increment() => Value++;
    }

    sealed class Runner {
        public static void BumpFirst(Counter[] counters) {
            ref readonly var counter = ref counters[0];
            counter.Increment();
        }
    }
}
