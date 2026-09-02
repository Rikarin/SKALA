// A property or a method already returns a copy, so the write was never going anywhere and the
// receiver is not one the language calls readonly. A different shape with a different repair.
namespace Fixtures {
    struct Counter {
        public int Value;

        public void Increment() => Value++;
    }

    sealed class Runner {
        Counter Current { get; }

        Counter Make() => default;

        public void Bump() {
            Current.Increment();
            Make().Increment();
        }
    }
}
