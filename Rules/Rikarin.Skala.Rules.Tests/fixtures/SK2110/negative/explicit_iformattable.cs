// IFormattable's ToString has its own contract, and it is not object's.
namespace Fixtures {
    sealed class Amount : System.IFormattable {
        string System.IFormattable.ToString(string? format, System.IFormatProvider? provider) => "amount";

        public override string ToString() => "amount";
    }
}
