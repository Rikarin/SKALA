namespace Fixtures {
    struct Counter {
        public int Value;

        public void Increment() {
            Value++;
        }
    }

    sealed class Runner {
        public static void Bump(ref readonly Counter counter) {
            counter.Increment();
        }
    }
}
