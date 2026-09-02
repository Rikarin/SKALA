using System;

namespace Fixtures {
    sealed class Reader {
        public static bool IsYes(ReadOnlySpan<char> value) => value == "yes";
    }
}
