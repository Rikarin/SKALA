// ⚠ A deliberate departure from the compiler. `readonly struct` accepts this — it writes a defensive
// copy — but when `T` is a struct the write lands in the capture today, which is the mutable hidden
// field this rule is about. Only a type known to be a reference type stops the climb.
namespace Fixtures {
    interface ICounter {
        int Count { get; set; }
    }

    sealed class Holder<T>(T counter)
        where T : ICounter {
        public void Reset() => counter.Count = 0;

        public int Count => counter.Count;
    }
}
