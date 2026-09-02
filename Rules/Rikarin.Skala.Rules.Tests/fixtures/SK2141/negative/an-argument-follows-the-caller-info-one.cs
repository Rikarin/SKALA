// The restatement is real and the deletion is not available: dropping this argument would move
// the one after it into the caller-info parameter. Only a trailing run is reported, so the rule
// declines rather than offering a fix that changes what the call means.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Trailing {
        public void Run() {
            Trace("started", nameof(Run), 7);
        }

        static void Trace(string message, [CallerMemberName] string? member = null, int retries = 0) { }
    }
}
