using System;

namespace Fixtures {
    sealed class Reader {
        public static bool Same(string left, string right) => left == right;

        public static bool SameArray(char[] left, char[] right) => left == right;

        public static bool SameMemory(ReadOnlyMemory<char> left, ReadOnlyMemory<char> right) => left.Equals(right);
    }
}
