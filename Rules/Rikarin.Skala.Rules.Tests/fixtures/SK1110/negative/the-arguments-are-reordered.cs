// The pass-through must be positional, in order, name for name. A call that reorders is doing work.
namespace Fixtures {
    sealed class Swapped {
        internal string Join(string left, string right) => Join(right, left, "-");

        internal string Join(string left, string right, string separator) => left + separator + right;
    }
}
