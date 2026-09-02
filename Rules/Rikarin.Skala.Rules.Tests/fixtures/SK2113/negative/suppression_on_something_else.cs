// Anti-vacuity: a `!` is not by itself this rule's shape.
#nullable disable
namespace Fixtures {
    sealed class Reader {
        public int Measure(string text) => text!.Length;
    }
}
