// A hand-written line number names a place the code is not, and nobody re-derives one by hand
// during review, so the wrong value survives every edit made above it.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Auditor {
        public void Run() {
            Record("started", 42);
        }

        static void Record(string message, [CallerLineNumber] int line = 0) { }
    }
}
