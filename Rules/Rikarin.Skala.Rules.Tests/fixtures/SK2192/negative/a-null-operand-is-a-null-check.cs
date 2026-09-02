using System;

namespace Fixtures {
    sealed class Reader {
        public static bool Missing(string? value) => value == null;

        public static bool Present(ReadOnlySpan<char> value) => value != default;
    }
}
