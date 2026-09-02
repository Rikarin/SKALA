// A `ref` or `in` receiver is state on the parameter that the block's receiver declaration would
// have to reproduce, and the conversion stops being subtractive.
namespace Fixtures {
    struct Counter {
        public int Value;
    }

    static class RefReceivers {
        public static void Bump(this ref Counter counter) => counter.Value++;

        public static void Add(this ref Counter counter, int amount) => counter.Value += amount;
    }
}
