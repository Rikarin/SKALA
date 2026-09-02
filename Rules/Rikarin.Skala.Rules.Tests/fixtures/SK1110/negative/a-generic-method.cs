// Excluded on either side: the type argument has to be reconciled with the deleted signature's, and
// inference at the call sites can change once the shorter candidate is gone.
namespace Fixtures {
    sealed class GenericPair {
        internal string Describe<T>(T value) => Describe(value, 4);

        internal string Describe<T>(T value, int indent) => new string(' ', indent) + value;
    }
}
