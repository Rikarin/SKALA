namespace Fixtures {
    struct Counter {
        public int Value;

        public void Report(System.Action<int> sink) => sink(Value);
    }

    sealed class Runner {
        public static void Show(in Counter counter) => counter.Report(static value => { });
    }
}
