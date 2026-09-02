using System;

namespace Fixtures {
    sealed class Reader {
        public static bool Same(ReadOnlySpan<char> left, ReadOnlySpan<char> right) => left.SequenceEqual(right);

        public static bool IsYes(ReadOnlySpan<char> value) => value.SequenceEqual("yes");
    }
}
