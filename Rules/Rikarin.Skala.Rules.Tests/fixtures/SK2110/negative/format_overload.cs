// A `ToString` that takes an argument is a different method with a different contract; the
// interpolation and logging assumption is about the parameterless override.
namespace Fixtures {
    sealed class Money {
        public string? ToString(string format) => null;

        public override string ToString() => "money";
    }
}
