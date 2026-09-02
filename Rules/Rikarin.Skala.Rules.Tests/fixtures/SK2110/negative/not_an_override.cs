// A method that merely returns a nullable string is not making ToString()'s promise.
namespace Fixtures {
    sealed class Report {
        public string? Describe() => null;

        public override string ToString() => "report";
    }
}
