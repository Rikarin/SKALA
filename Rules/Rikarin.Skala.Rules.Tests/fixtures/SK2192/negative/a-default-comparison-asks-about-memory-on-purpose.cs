using System;

namespace Fixtures {
    sealed class Reader {
        public static bool IsDefault(ReadOnlySpan<char> value) => value == default;

        public static bool IsAlsoDefault(ReadOnlySpan<char> value) => value != default(ReadOnlySpan<char>);
    }
}
