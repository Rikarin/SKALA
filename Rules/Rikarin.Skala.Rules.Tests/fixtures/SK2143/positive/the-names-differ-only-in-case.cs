// The match is case-insensitive on purpose: a parameter `source` filled by a field `Source` is
// the same evidence, and C#'s own naming conventions guarantee the case will differ whenever the
// argument is a property or a field.
namespace Fixtures {
    sealed class Transfer {
        string Source { get; } = string.Empty;

        string Destination { get; } = string.Empty;

        public void Run() {
            var source = Source;
            var destination = Destination;
            Copy(destination, source);
        }

        static void Copy(string Source, string Destination) { }
    }
}
