// ⚠ A `this ref` extension takes the capture by writable reference with no `ref` at the call site:
// CS9116 under `readonly`, the same write as `Helper(ref counter)` (#412).
namespace Fixtures {
    public struct Counter {
        public int Count;
    }

    public static class CounterExtensions {
        public static void Increment(this ref Counter counter) => counter.Count++;
    }

    struct Tally(Counter counter) {
        public void Bump() => counter.Increment();

        public int Count => counter.Count;
    }
}
