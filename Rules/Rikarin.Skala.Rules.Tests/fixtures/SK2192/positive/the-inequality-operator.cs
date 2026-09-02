using System;

namespace Fixtures {
    sealed class Reader {
        public static bool Differs(Span<byte> left, Span<byte> right) => left != right;
    }
}
