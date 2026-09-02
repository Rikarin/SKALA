namespace Fixtures {
    struct Counter {
        public int Value;

        public int Next() {
            Value++;
            return Value;
        }
    }

    sealed class Runner {
        public static int Read(in Counter counter) => counter.Next();
    }
}
