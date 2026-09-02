using System;

namespace Fixtures {
    sealed class Reader {
        public static bool Longer(ReadOnlySpan<char> left, ReadOnlySpan<char> right) => left.Length > right.Length;

        public static bool Empty(ReadOnlySpan<char> value) => value.IsEmpty;
    }
}
