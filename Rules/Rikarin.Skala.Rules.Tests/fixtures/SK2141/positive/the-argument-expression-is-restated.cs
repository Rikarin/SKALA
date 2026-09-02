// `nameof(value)` is the source text of the argument the attribute quotes, so the compiler would
// have produced the identical string.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Guard {
        public void Run(string value) {
            Check(value, nameof(value));
        }

        static void Check(object? argument, [CallerArgumentExpression("argument")] string? expression = null) { }
    }
}
