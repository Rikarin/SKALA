// No `using System;`, so `MemoryExtensions.SequenceEqual` is not in reach as an extension. The
// fix is bound before it is offered, and where it does not bind there is no finding rather than
// text that parses and will not compile.
namespace Fixtures {
    sealed class Reader {
        public static bool Same(System.ReadOnlySpan<char> left, System.ReadOnlySpan<char> right) => left == right;
    }
}
