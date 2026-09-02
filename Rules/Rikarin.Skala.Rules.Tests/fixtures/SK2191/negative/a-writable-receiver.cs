namespace Fixtures {
    struct Counter {
        public int Value;

        public void Increment() => Value++;
    }

    sealed class Runner {
        Counter field;

        public static void ByRef(ref Counter counter) => counter.Increment();

        public static void ByValue(Counter counter) => counter.Increment();

        public void Field() => field.Increment();

        public static void Local() {
            var counter = default(Counter);
            counter.Increment();
        }
    }
}
