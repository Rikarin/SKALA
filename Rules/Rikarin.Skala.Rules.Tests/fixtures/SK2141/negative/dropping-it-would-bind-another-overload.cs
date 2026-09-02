// ⚠ The restatement is real and deleting it would change which method is called. `Trace("a")`
// binds to the one-parameter overload, not to a shortened call of the two-parameter one, so the
// "fix" would silently move the call and the caller-info parameter would never be involved again.
//
// Every candidate deletion is re-bound speculatively and withdrawn unless the same symbol comes
// back — the guard SK0232 gets right, borrowed here for the same reason.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Overloaded {
        public void Run() {
            Trace("started", nameof(Run));
        }

        static void Trace(string message) { }

        static void Trace(string message, [CallerMemberName] string? member = null) { }
    }
}
