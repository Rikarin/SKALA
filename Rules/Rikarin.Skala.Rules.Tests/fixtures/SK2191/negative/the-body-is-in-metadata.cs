// A framework struct's method body is not in this file, so nothing has been read and nothing is
// claimed. Guessing at metadata is how a rule starts being "usually right".
using System.Collections.Generic;

namespace Fixtures {
    sealed class Runner {
        public static void Walk(in List<int>.Enumerator enumerator) => enumerator.Dispose();
    }
}
