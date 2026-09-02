// A named argument does not have to be last and does not have to fill the parameter its position
// names, so the trailing-run walk has no ground to stand on. Declined outright — the same guard
// SK0232 applies, for the same reason, and it is the right idea there.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Named {
        public void Run() {
            Trace(member: nameof(Run), message: "started");
        }

        static void Trace(string message, [CallerMemberName] string? member = null) { }
    }
}
