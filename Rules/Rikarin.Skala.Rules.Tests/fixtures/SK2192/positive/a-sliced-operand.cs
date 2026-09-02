using System;

namespace Fixtures {
    sealed class Reader {
        public static bool HeadMatches(ReadOnlySpan<char> value, ReadOnlySpan<char> prefix) =>
            value.Slice(0, prefix.Length) == prefix;
    }
}
