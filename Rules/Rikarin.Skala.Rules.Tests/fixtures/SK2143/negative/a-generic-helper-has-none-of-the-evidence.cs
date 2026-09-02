// ⚠ Two `T` arguments to a generic helper are the same shape with none of the evidence: the
// types agree because they are the *same type parameter*, not because the author chose two
// interchangeable things, and swapping is what half of these helpers are for.
namespace Fixtures {
    sealed class Pairs {
        public (T, T) Run<T>(T first, T second) => Swap(second, first);

        static (T, T) Swap<T>(T first, T second) => (second, first);
    }
}
