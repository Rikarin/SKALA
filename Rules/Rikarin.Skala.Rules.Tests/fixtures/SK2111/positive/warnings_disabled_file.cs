// No nullable warning can be issued anywhere in this file, so the `!` suppresses nothing and only
// makes the file look as though its nullability was considered.
#nullable disable
namespace Fixtures {
    sealed class Reader {
        public int Measure(string text) => text!.Length;
    }
}
