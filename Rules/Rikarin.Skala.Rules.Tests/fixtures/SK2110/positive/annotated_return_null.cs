// `object.ToString()` is itself annotated `string?`, so overriding it as `string?` and returning
// null is legal and the compiler says nothing at all. Every interpolation and every logger still
// assumes a string comes back.
namespace Fixtures {
    sealed class Ticket {
        public override string? ToString() => null;
    }
}
