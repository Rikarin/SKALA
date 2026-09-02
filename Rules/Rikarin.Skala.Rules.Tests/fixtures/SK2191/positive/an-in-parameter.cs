namespace Fixtures {
    struct Counter {
        public int Value;

        public void Increment() => Value++;
    }

    sealed class Runner {
        public static void Bump(in Counter counter) => counter.Increment();
    }
}
