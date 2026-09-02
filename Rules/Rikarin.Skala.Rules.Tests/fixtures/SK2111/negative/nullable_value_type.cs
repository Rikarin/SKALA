// `int?` is a value type and the `!` on it suppresses CS8629 at the `.Value`.
namespace Fixtures {
    sealed class Totals {
        public int Read(int? value) => value!.Value;
    }
}
