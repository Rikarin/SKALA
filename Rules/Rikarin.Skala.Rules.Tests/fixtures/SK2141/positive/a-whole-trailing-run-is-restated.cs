// ⚠ Two adjacent caller-info arguments go in one edit. Reported separately, the fix would delete
// the last one, the rule would fire again on its own output, and `skala fix` would loop.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Reporter {
        public void Run() {
            Report("started", nameof(Run), 42);
        }

        static void Report(
            string message,
            [CallerMemberName] string? member = null,
            [CallerLineNumber] int line = 0
        ) { }
    }
}
